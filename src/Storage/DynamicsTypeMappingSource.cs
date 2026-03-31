using System;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Storage;

public class DynamicsTypeMappingSource : TypeMappingSource
{
    public DynamicsTypeMappingSource(TypeMappingSourceDependencies dependencies) : base(dependencies)
    {
    }

    protected override CoreTypeMapping? FindMapping(in TypeMappingInfo mappingInfo)
    {
        if (mappingInfo.ClrType == null)
        {
            throw new InvalidOperationException($"Unable to determine CLR type for mappingInfo '{mappingInfo}'");
        }

        return FindPrimitiveMapping(mappingInfo) ?? base.FindMapping(mappingInfo);
    }

    private static DynamicsTypeMapping? FindPrimitiveMapping(in TypeMappingInfo mappingInfo)
    {
        var clrType = mappingInfo.ClrType!;

        if (clrType is { IsValueType: true } || clrType == typeof(string))
        {
            return new DynamicsTypeMapping(clrType);
        }

        return null;
    }
}