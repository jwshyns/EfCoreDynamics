using EfCore.Dynamics365.Query.Visitors;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query.Factories;

public class DynamicsQueryableMethodTranslatingExpressionVisitorFactory : IQueryableMethodTranslatingExpressionVisitorFactory
{
    public DynamicsQueryableMethodTranslatingExpressionVisitorFactory(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies)
    {
        Dependencies = dependencies;
    }

    protected virtual QueryableMethodTranslatingExpressionVisitorDependencies Dependencies { get; }

    public QueryableMethodTranslatingExpressionVisitor Create(IModel model) =>
        new DynamicsQueryableMethodTranslatingExpressionVisitor(Dependencies, model);
}