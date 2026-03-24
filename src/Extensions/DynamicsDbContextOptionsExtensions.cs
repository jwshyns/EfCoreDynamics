using System;
using EfCore.Dynamics365.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EfCore.Dynamics365
{
    /// <summary>
    /// Extension methods on <see cref="DbContextOptionsBuilder"/> for configuring
    /// the Dynamics 365 / Dataverse EF Core provider.
    /// </summary>
    public static class DynamicsDbContextOptionsExtensions
    {
        // ── Client-credentials (recommended for server-side) ──────────────────

        /// <summary>
        /// Configures EF Core to use Dynamics 365 / Dataverse with
        /// OAuth2 client-credentials (app registration).
        /// </summary>
        /// <param name="optionsBuilder">The options builder.</param>
        /// <param name="serviceUrl">
        ///   The Dataverse environment URL,
        ///   e.g. <c>https://yourorg.api.crm.dynamics.com</c>.
        /// </param>
        /// <param name="tenantId">Azure AD tenant GUID or domain.</param>
        /// <param name="clientId">Azure AD application (client) ID.</param>
        /// <param name="clientSecret">Client secret for the app registration.</param>
        /// <param name="optionsAction">Optional builder for additional options.</param>
        public static DbContextOptionsBuilder UseDynamics365(
            this DbContextOptionsBuilder optionsBuilder,
            string serviceUrl,
            string tenantId,
            string clientId,
            string clientSecret,
            Action<DynamicsDbOptionsBuilder>? optionsAction = null)
        {
            var ext = new DynamicsOptionsExtension()
                .WithServiceUrl(serviceUrl)
                .WithTenantId(tenantId)
                .WithClientId(clientId)
                .WithClientSecret(clientSecret);

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(ext);

            optionsAction?.Invoke(new DynamicsDbOptionsBuilder(optionsBuilder));
            return optionsBuilder;
        }

        /// <summary>
        /// Configures EF Core to use Dynamics 365 / Dataverse with username+password
        /// (ROPC flow). Only use for non-MFA accounts and development/testing scenarios.
        /// </summary>
        public static DbContextOptionsBuilder UseDynamics365WithPassword(
            this DbContextOptionsBuilder optionsBuilder,
            string serviceUrl,
            string tenantId,
            string clientId,
            string username,
            string password,
            Action<DynamicsDbOptionsBuilder>? optionsAction = null)
        {
            var ext = new DynamicsOptionsExtension()
                .WithServiceUrl(serviceUrl)
                .WithTenantId(tenantId)
                .WithClientId(clientId)
                .WithUsername(username)
                .WithPassword(password);

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(ext);

            optionsAction?.Invoke(new DynamicsDbOptionsBuilder(optionsBuilder));
            return optionsBuilder;
        }

        // ── Generic overload ──────────────────────────────────────────────────

        /// <summary>
        /// Configures EF Core to use Dynamics 365 / Dataverse with client credentials
        /// (generic <see cref="DbContextOptionsBuilder{TContext}"/> overload).
        /// </summary>
        public static DbContextOptionsBuilder<TContext> UseDynamics365<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            string serviceUrl,
            string tenantId,
            string clientId,
            string clientSecret,
            Action<DynamicsDbOptionsBuilder>? optionsAction = null)
            where TContext : DbContext
        {
            return (DbContextOptionsBuilder<TContext>)
                UseDynamics365(
                    (DbContextOptionsBuilder)optionsBuilder,
                    serviceUrl, tenantId, clientId, clientSecret,
                    optionsAction);
        }
    }
}
