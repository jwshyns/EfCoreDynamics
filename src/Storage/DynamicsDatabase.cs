using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EfCore.Dynamics365.Client;
using EfCore.Dynamics365.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using EfEntityState = Microsoft.EntityFrameworkCore.EntityState;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace EfCore.Dynamics365.Storage;

/// <summary>
/// Handles SaveChanges by translating EF Core change-tracker entries into
/// Dataverse create / update / delete SDK calls.
/// </summary>
internal sealed class DynamicsDatabase : Database
{
    private readonly IDynamicsClient _client;
    private readonly IDbContextTransactionManager _transactionManager;
    private readonly ICurrentDbContext _currentDbContext;

    public DynamicsDatabase(
        DatabaseDependencies dependencies,
        IDynamicsClient client,
        IDbContextTransactionManager transactionManager,
        ICurrentDbContext currentDbContext
    )
        : base(dependencies)
    {
        _client = client;
        _transactionManager = transactionManager;
        _currentDbContext = currentDbContext;
    }

    // ── Sync ──────────────────────────────────────────────────────────────

    public override int SaveChanges(IList<IUpdateEntry> entries)
    {
        // TODO: update to handle explicit and implicit transactions
        return SaveChangesAsync(entries).GetAwaiter().GetResult();
    }

    // ── Async ─────────────────────────────────────────────────────────────

    public override async Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default
    )
    {
        var deduplicatedEntries = entries
            .GroupBy(e => (e.EntityType.GetEntityLogicalName(), GetPrimaryKeyGuid(e, e.EntityType)))
            .Select(g => g.First())
            .ToList();

        var hasCurrentTransaction = _transactionManager.CurrentTransaction != null;

        // explicit transaction
        if (hasCurrentTransaction)
            return await CommitUpdates(deduplicatedEntries, true, cancellationToken).ConfigureAwait(false);

        var areAutoTransactionsEnabled = _currentDbContext.Context.Database.AutoTransactionsEnabled;
        var hasMultipleOperations = entries.Count > 1;

        // implicit transaction
        if (areAutoTransactionsEnabled && hasMultipleOperations)
            return await CommitUpdates(deduplicatedEntries, true, cancellationToken).ConfigureAwait(false);
        
        // multiple operations in a single request but not in a transaction
        if (hasMultipleOperations)
            return await CommitUpdates(deduplicatedEntries, false, cancellationToken).ConfigureAwait(false);

        // single operation in a single request
        return await CommitUpdate(deduplicatedEntries.First(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> CommitUpdate(IUpdateEntry entry, CancellationToken cancellationToken)
    {
        switch (entry.EntityState)
        {
            case EfEntityState.Added:
                await HandleAddedAsync(entry, cancellationToken);
                return 1;
            case EfEntityState.Modified:
                await HandleModifiedAsync(entry, entry.EntityType, cancellationToken);
                return 1;
            case EfEntityState.Deleted:
                await HandleDeletedAsync(entry, entry.EntityType, cancellationToken);
                return 1;
            // these don't require requests being made
            case EfEntityState.Detached:
            case EfEntityState.Unchanged:
                return 0;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task<int> CommitUpdates(
        IList<IUpdateEntry> entries,
        bool inTransaction,
        CancellationToken cancellationToken
    )
    {
        var requests = BuildRequests(entries);

        List<OrganizationResponse> responses;
        if (inTransaction)
            responses = (await _client.ExecuteTransactionAsync(requests, cancellationToken).ConfigureAwait(false))
                .Responses
                .ToList();
        else
            responses = (await _client.ExecuteMultipleAsync(requests, cancellationToken).ConfigureAwait(false))
                .Responses
                .Select(x => x.Response)
                .ToList();


        List<string> failures = [];
        for (var index = 0; index < responses.Count; index++)
        {
            var response = responses[index];
            var correlatingEntry = entries[index];
            switch (correlatingEntry.EntityState)
            {
                case EfEntityState.Added:
                    if (response is CreateResponse createResponse)
                        UpdateIdFromResponse(correlatingEntry, createResponse.id);
                    else
                        failures.Add($"Failed to create entity of type '{correlatingEntry.EntityType.Name}'.");
                    break;
                case EfEntityState.Modified:
                    if (response is not UpdateResponse)
                        failures.Add($"Failed to update entity of type '{correlatingEntry.EntityType.Name}'.");
                    break;
                case EfEntityState.Deleted:
                    if (response is not DeleteResponse)
                        failures.Add($"Failed to delete entity of type '{correlatingEntry.EntityType.Name}'.");
                    break;
                // there shouldn't be responses relevant to this
                case EfEntityState.Detached:
                case EfEntityState.Unchanged:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (failures.Count > 0) throw new Exception(string.Join(Environment.NewLine, failures));

        // TODO: determine if this is right
        return entries.Count;
    }

    private static OrganizationRequestCollection BuildRequests(IList<IUpdateEntry> entries)
    {
        OrganizationRequestCollection requests = [];

        foreach (var entry in entries)
            switch (entry.EntityState)
            {
                case EfEntityState.Added:
                    requests.Add(new CreateRequest { Target = BuildEntity(entry, false) });
                    break;
                case EfEntityState.Modified:
                    requests.Add(new UpdateRequest { Target = BuildEntity(entry, false) });
                    break;
                case EfEntityState.Deleted:
                    requests.Add(new DeleteRequest { Target = new EntityReference() });
                    break;
                // these don't require requests being made
                case EfEntityState.Detached:
                case EfEntityState.Unchanged:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        return requests;
    }

    // ── Operation handlers ────────────────────────────────────────────────

    private async Task HandleAddedAsync(IUpdateEntry entry, CancellationToken cancellationToken)
    {
        var entity = BuildEntity(entry, modified: false);
        var newId = await _client.CreateAsync(entity, cancellationToken).ConfigureAwait(false);

        // Write back the generated primary key so EF Core tracks the real ID.
        UpdateIdFromResponse(entry, newId);
    }

    private static void UpdateIdFromResponse(IUpdateEntry entry, Guid newId)
    {
        var pk = entry.EntityType.FindPrimaryKey();
        if (pk == null || newId == Guid.Empty) return;
        foreach (var keyProp in pk.Properties)
            if (keyProp.ClrType == typeof(Guid))
                entry.SetStoreGeneratedValue(keyProp, newId);
    }

    private async Task HandleModifiedAsync(
        IUpdateEntry entry,
        IEntityType entityType,
        CancellationToken cancellationToken
    )
    {
        var id = GetPrimaryKeyGuid(entry, entityType);
        var entity = BuildEntity(entry, modified: true);
        entity.Id = id;
        await _client.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleDeletedAsync(
        IUpdateEntry entry,
        IEntityType entityType,
        CancellationToken cancellationToken
    )
    {
        var id = GetPrimaryKeyGuid(entry, entityType);
        var logicalName = entityType.GetEntityLogicalName();
        await _client.DeleteAsync(logicalName, id, cancellationToken).ConfigureAwait(false);
    }

    // ── Payload builder ───────────────────────────────────────────────────

    /// <summary>
    /// Builds a Dataverse <see cref="Entity"/> from the EF Core change-tracker entry.
    /// For updates only modified non-key properties are included.
    /// </summary>
    private static Entity BuildEntity(IUpdateEntry entry, bool modified)
    {
        var entityType = entry.EntityType;
        var logicalName = entityType.GetEntityLogicalName();

        var entity = new Entity(logicalName);

        foreach (var prop in entityType.GetProperties())
        {
            if (prop.IsPrimaryKey())
            {
                if (!modified)
                {
                    // On insert: put the client-generated key into entity.Id.
                    var keyValue = entry.GetCurrentValue(prop);
                    if (keyValue is Guid g && g != Guid.Empty)
                        entity.Id = g;
                }

                continue; // PK never goes into Attributes.
            }

            if (modified && !entry.IsModified(prop)) continue;

            var logicalAttrName = prop.GetAttributeLogicalName()
                                  ?? prop.Name.ToLowerInvariant();

            entity.Attributes[logicalAttrName] = entry.GetCurrentValue(prop);
        }

        return entity;
    }

    // ── Key extraction ────────────────────────────────────────────────────

    private static Guid GetPrimaryKeyGuid(IUpdateEntry entry, IEntityType entityType)
    {
        var pk = entityType.FindPrimaryKey()
                 ?? throw new InvalidOperationException($"Entity {entityType.Name} has no primary key defined.");

        foreach (var keyProp in pk.Properties)
        {
            var value = entry.GetCurrentValue(keyProp);
            if (value is Guid g) return g;
            if (value is string s && Guid.TryParse(s, out var pg)) return pg;
        }

        throw new InvalidOperationException($"Could not extract a Guid primary key from {entityType.Name}.");
    }
}