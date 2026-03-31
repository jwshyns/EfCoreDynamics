using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Storage.Internal;

public class DynamicsTypeMappingSource : RelationalTypeMappingSource
{
    public DynamicsTypeMappingSource(TypeMappingSourceDependencies dependencies,
        RelationalTypeMappingSourceDependencies relationalDependencies) : base(dependencies, relationalDependencies)
    {
    }
}