using EfCore.Dynamics365.Client;
using EfCore.Dynamics365.Diagnostics;
using EfCore.Dynamics365.Infrastructure;
using EfCore.Dynamics365.Metadata.Conventions;
using EfCore.Dynamics365.Query;
using EfCore.Dynamics365.Query.Factories;
using EfCore.Dynamics365.Storage;
using EfCore.Dynamics365.ValueGeneration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace EfCore.Dynamics365.Extensions;

/// <summary>
/// Registers Dynamics 365 EF Core provider services into the DI container.
/// Called automatically by <see cref="Infrastructure.DynamicsOptionsExtension.ApplyServices"/>.
///
/// <para>
/// The consumer must register <see cref="IOrganizationServiceAsync2"/> independently, e.g.:
/// <code>
///   services.AddScoped&lt;IOrganizationServiceAsync2&gt;(_ => new ServiceClient(connectionString));
/// </code>
/// </para>
/// </summary>
public static class DynamicsServiceCollectionExtensions
{
    public static IServiceCollection AddEntityFrameworkDynamics365(this IServiceCollection services)
    {
        var builder = new EntityFrameworkServicesBuilder(services)
            .TryAdd<LoggingDefinitions, DynamicsLoggingDefinitions>()
            .TryAdd<IDatabaseProvider, DatabaseProvider<DynamicsOptionsExtension>>()
            .TryAdd<IDatabase, DynamicsDatabase>()
            .TryAdd<IDbContextTransactionManager, DynamicsTransactionManager>()
            .TryAdd<IModelValidator, DynamicsModelValidator>()
            .TryAdd<IProviderConventionSetBuilder, DynamicsConventionSetBuilder>()
            .TryAdd<IValueGeneratorSelector, DynamicsValueGeneratorSelector>()
            .TryAdd<IDatabaseCreator>(p => p.GetRequiredService<IDynamicsDatabaseCreator>())
            .TryAdd<IQueryContextFactory, DynamicsQueryContextFactory>()
            .TryAdd<ITypeMappingSource, DynamicsTypeMappingSource>()
            .TryAdd<IValueGeneratorSelector, DynamicsValueGeneratorSelector>()
            .TryAdd<IQueryTranslationPreprocessorFactory, DynamicsQueryTranslationPreprocessorFactory>()
            .TryAdd<IQueryCompilationContextFactory, DynamicsQueryCompilationContextFactory>()
            .TryAdd<IQueryTranslationPostprocessorFactory, DynamicsQueryTranslationPostprocessorFactory>()
            .TryAdd<IQueryableMethodTranslatingExpressionVisitorFactory,
                DynamicsQueryableMethodTranslatingExpressionVisitorFactory>()
            .TryAdd<IShapedQueryCompilingExpressionVisitorFactory,
                DynamicsShapedQueryCompilingExpressionVisitorFactory>()
            .TryAddProviderSpecificServices(b => b
                .TryAddScoped<IDynamicsClient, DynamicsClient>()
                .TryAddScoped<ITransactionEnlistmentManager, DynamicsTransactionEnlistmentManager>()
                .TryAddScoped<IDynamicsDatabaseCreator, DynamicsDatabaseCreator>()
            );

        builder.TryAddCoreServices();

        return services;
    }
}