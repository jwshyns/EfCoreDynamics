using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EfCore.Dynamics365.Storage.ValueConversion;

public class DynamicsConverterSelector : ValueConverterSelector
{
    public DynamicsConverterSelector(ValueConverterSelectorDependencies dependencies) : base(dependencies)
    {
    }
}