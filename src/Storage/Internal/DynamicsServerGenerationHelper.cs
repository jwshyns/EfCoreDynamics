using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Storage.Internal;

public class DynamicsServerGenerationHelper : RelationalSqlGenerationHelper
{
    public DynamicsServerGenerationHelper(RelationalSqlGenerationHelperDependencies dependencies) : base(dependencies)
    {
    }
}