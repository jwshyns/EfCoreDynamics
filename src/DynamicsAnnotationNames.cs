namespace EfCore.Dynamics365;

/// <summary>
/// EF Core annotation names used to store Dynamics 365 metadata on model elements.
/// </summary>
public static class DynamicsAnnotationNames
{
    private const string Prefix = "Dynamics365:";

    /// <summary>
    /// OData entity-set name used in Web API URLs, e.g. "accounts".
    /// Applied to IEntityType.
    /// </summary>
    public const string EntitySetName = Prefix + "EntitySetName";

    /// <summary>
    /// Logical name of the Dynamics entity, e.g. "account".
    /// Applied to IEntityType.
    /// </summary>
    public const string EntityLogicalName = Prefix + "EntityLogicalName";

    /// <summary>
    /// Logical name of the attribute, e.g. "accountid", "name".
    /// Applied to IProperty.
    /// </summary>
    public const string AttributeLogicalName = Prefix + "AttributeLogicalName";
}