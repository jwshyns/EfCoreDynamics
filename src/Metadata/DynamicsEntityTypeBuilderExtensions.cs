using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Dynamics365.Metadata;

/// <summary>
/// Fluent API extensions for configuring Dynamics 365 metadata
/// via <see cref="EntityTypeBuilder{TEntity}"/> in <c>OnModelCreating</c>.
/// </summary>
public static class DynamicsEntityTypeBuilderExtensions
{
    /// <summary>
    /// Sets the OData entity-set name used in Dataverse Web API URLs.
    /// <example><code>
    /// modelBuilder.Entity&lt;Account&gt;().HasEntitySetName("accounts");
    /// </code></example>
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasEntitySetName<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        string entitySetName)
        where TEntity : class
    {
        builder.Metadata.SetEntitySetName(entitySetName);
        return builder;
    }

    /// <summary>
    /// Sets the Dynamics 365 logical name for this entity type.
    /// <example><code>
    /// modelBuilder.Entity&lt;Account&gt;().HasDynamicsLogicalName("account");
    /// </code></example>
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasDynamicsLogicalName<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        string logicalName)
        where TEntity : class
    {
        builder.Metadata.SetEntityLogicalName(logicalName);
        return builder;
    }

    /// <summary>
    /// Non-generic overload of <see cref="HasEntitySetName{TEntity}"/>.
    /// </summary>
    public static EntityTypeBuilder HasEntitySetName(
        this EntityTypeBuilder builder,
        string entitySetName)
    {
        builder.Metadata.SetEntitySetName(entitySetName);
        return builder;
    }
}

/// <summary>
/// Fluent API extensions for <see cref="PropertyBuilder"/>.
/// </summary>
public static class DynamicsPropertyBuilderExtensions
{
    /// <summary>
    /// Sets the Dynamics 365 attribute logical name for this property.
    /// <example><code>
    /// modelBuilder.Entity&lt;Account&gt;()
    ///     .Property(e => e.Id)
    ///     .HasAttributeLogicalName("accountid");
    /// </code></example>
    /// </summary>
    public static PropertyBuilder<TProperty> HasAttributeLogicalName<TProperty>(
        this PropertyBuilder<TProperty> builder,
        string logicalName)
    {
        builder.Metadata.SetAttributeLogicalName(logicalName);
        return builder;
    }

    /// <summary>
    /// Non-generic overload.
    /// </summary>
    public static PropertyBuilder HasAttributeLogicalName(
        this PropertyBuilder builder,
        string logicalName)
    {
        builder.Metadata.SetAttributeLogicalName(logicalName);
        return builder;
    }
}