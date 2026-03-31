using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query.Factories;

public class DynamicsQueryTranslationPostprocessorFactory : IQueryTranslationPostprocessorFactory
{
    public DynamicsQueryTranslationPostprocessorFactory(QueryTranslationPostprocessorDependencies dependencies)
    {
        Dependencies = dependencies;
    }

    protected QueryTranslationPostprocessorDependencies Dependencies { get; }

    public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext) =>
        new DynamicsQueryTranslationPostprocessor(Dependencies, queryCompilationContext);
}