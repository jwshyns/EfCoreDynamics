using System;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EfCore.Dynamics365.Storage;

public class DynamicsTypeMapping : CoreTypeMapping
{
    public DynamicsTypeMapping(
        Type clrType,
        ValueComparer? comparer = null,
        ValueComparer? keyComparer = null
    ) : base(new CoreTypeMappingParameters(clrType, converter: null, comparer, keyComparer))
    {
    }

    public override CoreTypeMapping Clone(ValueConverter? converter)
    {
        return new DynamicsTypeMapping(ClrType, Comparer, KeyComparer);
    }
}