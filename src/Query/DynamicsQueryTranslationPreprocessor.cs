using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query;

/// <summary>
/// Preprocesses the LINQ expression tree before the main translation phase.
/// Delegates entirely to the base class (include expansion, owned-type navigation, etc.).
/// </summary>
public sealed class DynamicsQueryTranslationPreprocessor : QueryTranslationPreprocessor
{
    public DynamicsQueryTranslationPreprocessor(
        QueryTranslationPreprocessorDependencies dependencies,
        QueryCompilationContext queryCompilationContext
    ) : base(dependencies, queryCompilationContext)
    {
    }
}