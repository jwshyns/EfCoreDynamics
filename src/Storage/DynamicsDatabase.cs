using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EfCore.Dynamics365.Client;
using EfCore.Dynamics365.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using EfEntityState = Microsoft.EntityFrameworkCore.EntityState;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Xrm.Sdk;

namespace EfCore.Dynamics365.Storage;

/// <summary>
/// Handles SaveChanges by translating EF Core change-tracker entries into
/// Dataverse create / update / delete SDK calls.
/// </summary>
public sealed class DynamicsDatabase : Database
{
    private readonly DynamicsCrmClient _client;

    public DynamicsDatabase(
        DatabaseDependencies dependencies,
        DynamicsCrmClient client)
        : base(dependencies)
    {
        _client = client;
    }

    // ── Sync ──────────────────────────────────────────────────────────────

    public override int SaveChanges(IList<IUpdateEntry> entries) => SaveChangesAsync(entries).GetAwaiter().GetResult();

    // ── Async ─────────────────────────────────────────────────────────────

    public override async Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var saved = 0;

        foreach (var entry in entries)
        {
            var entityType = entry.EntityType;

            switch (entry.EntityState)
            {
                case EfEntityState.Added:
                    await HandleAddedAsync(entry, entityType, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case EfEntityState.Modified:
                    await HandleModifiedAsync(entry, entityType, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case EfEntityState.Deleted:
                    await HandleDeletedAsync(entry, entityType, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case EfEntityState.Detached:
                case EfEntityState.Unchanged:
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            saved++;
        }

        return saved;
    }

    // ── Operation handlers ────────────────────────────────────────────────

    private async Task HandleAddedAsync(
        IUpdateEntry entry,
        IEntityType entityType,
        CancellationToken cancellationToken)
    {
        var entity = BuildEntity(entry, entityType, modified: false);
        var newId  = await _client.CreateAsync(entity, cancellationToken).ConfigureAwait(false);

        // Write back the generated primary key so EF Core tracks the real ID.
        var pk = entityType.FindPrimaryKey();
        if (pk != null && newId != Guid.Empty)
        {
            foreach (var keyProp in pk.Properties)
            {
                if (keyProp.ClrType == typeof(Guid))
                    entry.SetStoreGeneratedValue(keyProp, newId);
            }
        }
    }

    private async Task HandleModifiedAsync(
        IUpdateEntry entry,
        IEntityType entityType,
        CancellationToken cancellationToken)
    {
        var id     = GetPrimaryKeyGuid(entry, entityType);
        var entity = BuildEntity(entry, entityType, modified: true);
        entity.Id  = id;
        await _client.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleDeletedAsync(
        IUpdateEntry entry,
        IEntityType entityType,
        CancellationToken cancellationToken)
    {
        var id          = GetPrimaryKeyGuid(entry, entityType);
        var logicalName = entityType.GetEntityLogicalName()
                          ?? entityType.ClrType.Name.ToLowerInvariant();
        await _client.DeleteAsync(logicalName, id, cancellationToken).ConfigureAwait(false);
    }

    // ── Payload builder ───────────────────────────────────────────────────

    /// <summary>
    /// Builds a Dataverse <see cref="Entity"/> from the EF Core change-tracker entry.
    /// For updates only modified non-key properties are included.
    /// </summary>
    internal static Entity BuildEntity(
        IUpdateEntry entry,
        IEntityType entityType,
        bool modified)
    {
        var logicalName = entityType.GetEntityLogicalName()
                          ?? entityType.ClrType.Name.ToLowerInvariant();

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
