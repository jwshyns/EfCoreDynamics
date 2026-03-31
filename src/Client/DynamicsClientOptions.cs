using System;

namespace EfCore.Dynamics365.Client;

/// <summary>
/// Lightweight options bag. The actual Dataverse connection is provided by
/// the consumer via <c>IOrganizationService</c> registered in the DI container.
/// </summary>
public class DynamicsClientOptions
{
    /// <summary>
    /// The root service URL, e.g. https://yourorg.api.crm.dynamics.com
    /// Used for display / logging only.
    /// </summary>
    public string ServiceUrl { get; init; } = string.Empty;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServiceUrl))
            throw new InvalidOperationException("DynamicsClientOptions.ServiceUrl must be set.");
    }
}