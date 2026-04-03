using EfCore.Dynamics365.Client;
using EfCore.Dynamics365.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace EfCore.Dynamics365.Extensions;

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

    public static DbContextOptionsBuilder UseDynamics365(
        this DbContextOptionsBuilder optionsBuilder,
        IOrganizationServiceAsync2 organizationServiceAsync2
    )
    {
        var ext = new DynamicsOptionsExtension().WithOrganisationAsync2(organizationServiceAsync2);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(ext);
        return optionsBuilder;
    }
    
    public static DbContextOptionsBuilder<TContext> UseDynamics365<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        IOrganizationServiceAsync2 organizationServiceAsync2
    )  where TContext : DbContext
    {
        return (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseDynamics365(
            organizationServiceAsync2);
    }

    /// <summary>Generic <see cref="DbContextOptionsBuilder{TContext}"/> overload.</summary>
    public static DbContextOptionsBuilder<TContext> UseDynamics365<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString
    ) where TContext : DbContext
    {
        return (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseDynamics365(
            connectionString);
    }
}