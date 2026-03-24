using System;

namespace EfCore.Dynamics365.Client
{
    /// <summary>
    /// Connection options for the Dataverse / Dynamics 365 Web API.
    /// Supports both client-credentials (app registration) and username/password auth.
    /// </summary>
    public class DynamicsClientOptions
    {
        /// <summary>
        /// The root service URL, e.g. https://yourorg.api.crm.dynamics.com
        /// </summary>
        public string ServiceUrl { get; set; } = string.Empty;

        /// <summary>
        /// The Azure AD tenant ID (GUID or domain).
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// The Azure AD application (client) ID.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// The client secret for client-credentials flow.
        /// Mutually exclusive with Username/Password.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Username for ROPC (resource owner password credentials) flow.
        /// Only for non-MFA accounts. Prefer client credentials in production.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Password for ROPC flow.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// The Dataverse Web API version to use, e.g. "v9.2".
        /// </summary>
        public string ApiVersion { get; set; } = "v9.2";

        /// <summary>
        /// HTTP timeout for individual requests.
        /// </summary>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(120);

        internal string ApiBaseUrl =>
            ServiceUrl.TrimEnd('/') + "/api/data/" + ApiVersion + "/";

        internal string Authority =>
            "https://login.microsoftonline.com/" + TenantId;

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(ServiceUrl))
                throw new InvalidOperationException("DynamicsClientOptions.ServiceUrl must be set.");
            if (string.IsNullOrWhiteSpace(TenantId))
                throw new InvalidOperationException("DynamicsClientOptions.TenantId must be set.");
            if (string.IsNullOrWhiteSpace(ClientId))
                throw new InvalidOperationException("DynamicsClientOptions.ClientId must be set.");
            if (string.IsNullOrWhiteSpace(ClientSecret) &&
                (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password)))
                throw new InvalidOperationException(
                    "Either ClientSecret (client-credentials) or Username+Password (ROPC) must be provided.");
        }
    }
}
