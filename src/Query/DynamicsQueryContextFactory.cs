using EfCore.Dynamics365.Client;
using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query
{
    /// <inheritdoc />
    public sealed class DynamicsQueryContextFactory : IQueryContextFactory
    {
        private readonly QueryContextDependencies _dependencies;
        private readonly DynamicsHttpClient _client;

        public DynamicsQueryContextFactory(
            QueryContextDependencies dependencies,
            DynamicsHttpClient client)
        {
            _dependencies = dependencies;
            _client       = client;
        }

        public QueryContext Create() =>
            new DynamicsQueryContext(_dependencies, _client);
    }
}
