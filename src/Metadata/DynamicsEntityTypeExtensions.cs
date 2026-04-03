using System;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCore.Dynamics365.Metadata;

/// <summary>
/// Extension methods for reading and writing Dynamics 365 metadata
/// on <see cref="IEntityType"/> and <see cref="IProperty"/>.
/// </summary>
public static class DynamicsEntityTypeExtensions
{
    // ── Entity set name ───────────────────────────────────────────────────

    /// <summary>
    /// Gets the OData entity-set name used in Web API URLs (e.g. "accounts").
    /// Falls back to the lower-cased, pluralised CLR type name if not set.
    /// </summary>
    public static string? GetEntitySetName(this IEntityType entityType)
    {
        var annotation = entityType.FindAnnotation(DynamicsAnnotationNames.EntitySetName);
        if (annotation?.Value is string value) return value;

        // Convention: lower-case plural of type name
        return Pluralise(entityType.ClrType.Name.ToLowerInvariant());
    }

    /// <summary>
    /// Sets the OData entity-set name on a mutable entity type.
    /// </summary>
    public static void SetEntitySetName(this IMutableEntityType entityType, string entitySetName)
        => entityType.SetAnnotation(DynamicsAnnotationNames.EntitySetName, entitySetName);

    // ── Logical name ──────────────────────────────────────────────────────

    /// <summary>
    /// Gets the Dynamics entity logical name (e.g. "account").
    /// Falls back to the lower-cased CLR type name if not set.
    /// </summary>
    public static string GetEntityLogicalName(this IEntityType entityType)
    {
        var annotation = entityType.FindAnnotation(DynamicsAnnotationNames.EntityLogicalName);
        if (annotation?.Value is string value) return value;

        return entityType.ClrType.Name.ToLowerInvariant();
    }

    /// <summary>
    /// Sets the Dynamics entity logical name on a mutable entity type.
    /// </summary>
    public static void SetEntityLogicalName(this IMutableEntityType entityType, string logicalName)
        => entityType.SetAnnotation(DynamicsAnnotationNames.EntityLogicalName, logicalName);

    // ── Attribute logical name ────────────────────────────────────────────

    /// <summary>
    /// Gets the Dynamics attribute logical name for a property (e.g. "accountid").
    /// Falls back to lower-case CLR property name if not set.
    /// </summary>
    public static string? GetAttributeLogicalName(this IProperty property)
    {
        var annotation = property.FindAnnotation(DynamicsAnnotationNames.AttributeLogicalName);
        if (annotation?.Value is string value) return value;

        return property.Name.ToLowerInvariant();
    }

    /// <summary>
    /// Sets the Dynamics attribute logical name on a mutable property.
    /// </summary>
    public static void SetAttributeLogicalName(this IMutableProperty property, string logicalName)
        => property.SetAnnotation(DynamicsAnnotationNames.AttributeLogicalName, logicalName);

    // ── Simple pluralisation (English) ────────────────────────────────────

    private static string Pluralise(string name)
    {
        if (name.EndsWith("y", StringComparison.Ordinal))
            return name.Substring(0, name.Length - 1) + "ies";
        if (name.EndsWith("s", StringComparison.Ordinal)
            || name.EndsWith("sh", StringComparison.Ordinal)
            || name.EndsWith("ch", StringComparison.Ordinal)
            || name.EndsWith("x", StringComparison.Ordinal))
            return name + "es";
        return name + "s";
    }
}