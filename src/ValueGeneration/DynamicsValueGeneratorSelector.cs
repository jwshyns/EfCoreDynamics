using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace EfCore.Dynamics365.ValueGeneration
{
    /// <summary>
    /// Selects value generators for Dynamics 365 entities.
    /// Guid primary keys (the only PK type supported by Dataverse) are generated
    /// client-side using <see cref="SequentialGuidValueGenerator"/>.
    /// </summary>
    public sealed class DynamicsValueGeneratorSelector : ValueGeneratorSelector
    {
        public DynamicsValueGeneratorSelector(ValueGeneratorSelectorDependencies dependencies) : base(dependencies) { }

        public override ValueGenerator Select(IProperty property, IEntityType entityType)
        {
            if (property.ClrType == typeof(Guid) || property.ClrType == typeof(Guid?))
                return new SequentialGuidValueGenerator();

            return base.Select(property, entityType);
        }
    }
}
