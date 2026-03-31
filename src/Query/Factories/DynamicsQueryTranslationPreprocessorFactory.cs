using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query.Factories;

public sealed class DynamicsQueryTranslationPreprocessorFactory : IQueryTranslationPreprocessorFactory
{
    private readonly QueryTranslationPreprocessorDependencies _dependencies;

    public DynamicsQueryTranslationPreprocessorFactory(QueryTranslationPreprocessorDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    public QueryTranslationPreprocessor Create(QueryCompilationContext queryCompilationContext)
        => new DynamicsQueryTranslationPreprocessor(_dependencies, queryCompilationContext);
}