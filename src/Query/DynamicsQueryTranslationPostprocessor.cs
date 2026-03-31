using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query;

public class DynamicsQueryTranslationPostprocessor : QueryTranslationPostprocessor
{
    private readonly QueryCompilationContext _queryCompilationContext;

    public DynamicsQueryTranslationPostprocessor(
        QueryTranslationPostprocessorDependencies dependencies,
        QueryCompilationContext queryCompilationContext
    ) : base(dependencies)
    {
        _queryCompilationContext = queryCompilationContext;
    }
}