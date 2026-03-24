using System.Threading;
using System.Threading.Tasks;
using EfCore.Dynamics365.Client;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Storage
{
    /// <summary>
    /// Dataverse does not require schema creation — this implementation
    /// is a no-op that always reports the "database" as existing.
    /// </summary>
    public sealed class DynamicsDatabaseCreator : IDatabaseCreator
    {
        private readonly DynamicsHttpClient _client;

        public DynamicsDatabaseCreator(DynamicsHttpClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Dataverse environments cannot be created via the Web API.
        /// This method always returns <c>false</c>.
        /// </summary>
        public bool EnsureCreated() => false;

        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        /// <summary>
        /// Dataverse environments cannot be deleted via the Web API.
        /// This method always returns <c>false</c>.
        /// </summary>
        public bool EnsureDeleted() => false;

        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        /// <summary>
        /// Returns <c>true</c> — the Dataverse environment is presumed to exist
        /// if a valid ServiceUrl is configured.
        /// </summary>
        public bool CanConnect() => true;

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
