using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query.Factories;

public class DynamicsQueryCompilationContextFactory : IQueryCompilationContextFactory
{
    public DynamicsQueryCompilationContextFactory(QueryCompilationContextDependencies dependencies)
    {
        Dependencies = dependencies;
    }

    protected virtual QueryCompilationContextDependencies Dependencies { get; }

    public QueryCompilationContext Create(bool async) => new DynamicsQueryCompilationContext(Dependencies, async);
}