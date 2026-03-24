using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EfCore.Dynamics365.Client;
using EfCore.Dynamics365.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace EfCore.Dynamics365.Storage
{
    /// <summary>
    /// Handles SaveChanges by translating EF Core change-tracker entries into
    /// Dataverse Web API create / update / delete calls.
    /// The base <see cref="Database"/> class provides <c>CompileQuery&lt;TResult&gt;</c>
    /// automatically using the registered query pipeline services.
    /// </summary>
    public sealed class DynamicsDatabase : Database
    {
        private readonly DynamicsHttpClient _client;

        public DynamicsDatabase(
            DatabaseDependencies dependencies,
            DynamicsHttpClient client)
            : base(dependencies)
        {
            _client = client;
        }

        // ── Sync ──────────────────────────────────────────────────────────────

        public override int SaveChanges(IList<IUpdateEntry> entries)
        {
            return SaveChangesAsync(entries).GetAwaiter().GetResult();
        }

        // ── Async ─────────────────────────────────────────────────────────────

        public override async Task<int> SaveChangesAsync(
            IList<IUpdateEntry> entries,
            CancellationToken cancellationToken = default)
        {
            var saved = 0;

            foreach (var entry in entries)
            {
                var entityType  = entry.EntityType;
                var entitySet   = entityType.GetEntitySetName()
                    ?? throw new InvalidOperationException(
                        $"No Dynamics 365 entity-set name for {entityType.Name}. " +
                        "Call .HasEntitySetName() in OnModelCreating.");

                switch (entry.EntityState)
                {
                    case EntityState.Added:
                        await HandleAddedAsync(entry, entityType, entitySet, cancellationToken)
                            .ConfigureAwait(false);
                        break;

                    case EntityState.Modified:
                        await HandleModifiedAsync(entry, entityType, entitySet, cancellationToken)
                            .ConfigureAwait(false);
                        break;

                    case EntityState.Deleted:
                        await HandleDeletedAsync(entry, entityType, entitySet, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case EntityState.Detached:
                        break;
                    case EntityState.Unchanged:
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
            string entitySet,
            CancellationToken ct)
        {
            var payload = BuildPayload(entry, entityType, modified: false);
            var newId   = await _client.CreateAsync(entitySet, payload, ct).ConfigureAwait(false);

            // Write back the generated primary key
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
            string entitySet,
            CancellationToken ct)
        {
            var id      = GetPrimaryKeyGuid(entry, entityType);
            var payload = BuildPayload(entry, entityType, modified: true);
            await _client.UpdateAsync(entitySet, id, payload, ct).ConfigureAwait(false);
        }

        private async Task HandleDeletedAsync(
            IUpdateEntry entry,
            IEntityType entityType,
            string entitySet,
            CancellationToken ct)
        {
            var id = GetPrimaryKeyGuid(entry, entityType);
            await _client.DeleteAsync(entitySet, id, ct).ConfigureAwait(false);
        }

        // ── Payload builders ──────────────────────────────────────────────────

        /// <summary>
        /// Builds a dictionary of {logicalName → value} for create or update.
        /// For updates, only includes modified properties.
        /// </summary>
        private static Dictionary<string, object?> BuildPayload(
            IUpdateEntry entry,
            IEntityType entityType,
            bool modified)
        {
            var dict = new Dictionary<string, object?>();

            foreach (var prop in entityType.GetProperties())
            {
                // Skip key properties on update (immutable in Dynamics)
                if (modified && prop.IsPrimaryKey()) continue;

                // For updates only include changed properties
                if (modified && !entry.IsModified(prop)) continue;

                var logicalName = prop.GetAttributeLogicalName()
                                  ?? prop.Name.ToLowerInvariant();

                var value = entry.GetCurrentValue(prop);
                dict[logicalName] = value;
            }

            return dict;
        }

        private static Guid GetPrimaryKeyGuid(IUpdateEntry entry, IEntityType entityType)
        {
            var pk = entityType.FindPrimaryKey()
                ?? throw new InvalidOperationException(
                    $"Entity {entityType.Name} has no primary key defined.");

            foreach (var keyProp in pk.Properties)
            {
                var value = entry.GetCurrentValue(keyProp);
                if (value is Guid g) return g;
                if (value is string s && Guid.TryParse(s, out var pg)) return pg;
            }

            throw new InvalidOperationException(
                $"Could not extract a Guid primary key from {entityType.Name}.");
        }
    }
}
