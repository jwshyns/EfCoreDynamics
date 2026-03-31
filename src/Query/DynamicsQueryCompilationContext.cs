using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query;

public class DynamicsQueryCompilationContext : QueryCompilationContext
{
    public DynamicsQueryCompilationContext(QueryCompilationContextDependencies dependencies, bool async) : base(
        dependencies, async)
    {
    }
}