using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCore.Dynamics365.Infrastructure;

public class DynamicsModelValidator : ModelValidator
{
    public DynamicsModelValidator(ModelValidatorDependencies dependencies) : base(dependencies)
    {
    }

    public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        base.Validate(model, logger);

        // TODO: add proper validation
        ValidateEntityLogicalName(model);
    }

    private static void ValidateEntityLogicalName(IModel model)
    {
        // foreach (var entityType in model.GetEntityTypes())
        // {
        //     var entityLogicalName = (string?)entityType.FindAnnotation("EntityLogicalName")?.Value;
        //     if (string.IsNullOrWhiteSpace(entityLogicalName))
        //         throw new NotSupportedException(
        //             $"Entity '{entityType.DisplayName()}' does not specify an entity logical name.");
        // }
    }
}