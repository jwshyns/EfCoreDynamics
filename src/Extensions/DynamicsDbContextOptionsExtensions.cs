using EfCore.Dynamics365.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EfCore.Dynamics365.Extensions;

/// <summary>
/// Extension methods on <see cref="DbContextOptionsBuilder"/> for configuring
/// the Dynamics 365 / Dataverse EF Core provider.
///
/// <para>
/// After calling <c>UseDynamics365</c>, register your <c>IOrganizationService</c>
/// implementation in the DI container:
/// <code>
///   services.AddScoped&lt;IOrganizationService&gt;(_ => new ServiceClient(connectionString));
/// </code>
/// </para>
/// </summary>
public static class DynamicsDbContextOptionsExtensions
{
    /// <summary>
    /// Configures EF Core to use Dynamics 365 / Dataverse via <c>IOrganizationService</c>.
    /// </summary>
    /// <param name="optionsBuilder">The options builder.</param>
    /// <param name="connectionString">
    ///   The Dataverse environment URL (used for logging/display only),
    ///   e.g. <c>https://yourorg.api.crm.dynamics.com</c>.
    /// </param>
    public static DbContextOptionsBuilder UseDynamics365(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString
    )
    {
        var ext = new DynamicsOptionsExtension().WithConnectionString(connectionString);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(ext);
        return optionsBuilder;
    }

    /// <summary>Generic <see cref="DbContextOptionsBuilder{TContext}"/> overload.</summary>
    public static DbContextOptionsBuilder<TContext> UseDynamics365<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString
    ) where TContext : DbContext
    {
        return (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseDynamics365(connectionString);
    }
}