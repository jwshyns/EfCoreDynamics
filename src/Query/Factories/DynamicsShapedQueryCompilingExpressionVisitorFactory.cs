using EfCore.Dynamics365.Query.Visitors;
using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query.Factories;

public class DynamicsShapedQueryCompilingExpressionVisitorFactory : IShapedQueryCompilingExpressionVisitorFactory
{
    public DynamicsShapedQueryCompilingExpressionVisitorFactory(
        ShapedQueryCompilingExpressionVisitorDependencies dependencies)
    {
        Dependencies = dependencies;
    }

    protected virtual ShapedQueryCompilingExpressionVisitorDependencies Dependencies { get; }

    public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext queryCompilationContext) =>
        new DynamicsShapedQueryCompilingExpressionVisitor(Dependencies, queryCompilationContext);
}