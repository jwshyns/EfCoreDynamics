using EfCore.Dynamics365.Client;
using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query;

/// <inheritdoc />
internal sealed class DynamicsQueryContextFactory : IQueryContextFactory
{
    private readonly QueryContextDependencies _dependencies;
    private readonly IDynamicsClient _client;

    public DynamicsQueryContextFactory(
        QueryContextDependencies dependencies,
        IDynamicsClient client)
    {
        _dependencies = dependencies;
        _client = client;
    }

    public QueryContext Create() => new DynamicsQueryContext(_dependencies, _client);
}