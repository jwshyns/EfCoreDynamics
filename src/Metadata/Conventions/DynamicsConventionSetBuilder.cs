using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace EfCore.Dynamics365.Metadata.Conventions;

public class DynamicsConventionSetBuilder : ProviderConventionSetBuilder
{
    public DynamicsConventionSetBuilder(ProviderConventionSetBuilderDependencies dependencies) : base(dependencies)
    {
    }

    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();
        
        // TODO: add reasonable conventions

        return conventionSet;
    }
}