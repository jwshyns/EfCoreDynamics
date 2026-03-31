using System.Diagnostics.CodeAnalysis;
using EfCore.Dynamics365.Client;
using EfCore.Dynamics365.Diagnostics;
using EfCore.Dynamics365.Infrastructure;
using EfCore.Dynamics365.Query;
using EfCore.Dynamics365.Storage;
using EfCore.Dynamics365.Storage.Internal;
using EfCore.Dynamics365.ValueGeneration;
using EfCore.Dynamics365.ValueGeneration.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
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
        var builder = new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<LoggingDefinitions, DynamicsLoggingDefinitions>()
            .TryAdd<IDatabaseProvider, DatabaseProvider<DynamicsOptionsExtension>>()
            .TryAdd<IValueGeneratorCache>(p => p.GetService<IDynamicsValueGeneratorCache>())
            .TryAdd<IRelationalTypeMappingSource, DynamicsTypeMappingSource>()
            .TryAdd<ISqlGenerationHelper, DynamicsSqlGenerationHelper>()
            .TryAdd<IMigrationsAnnotationProvider, DynamicsMigrationsAnnotationProvider>()
            .TryAdd<IModelValidator, DynamicsModelValidator>()
            .TryAdd<IProviderConventionSetBuilder, DynamicsConventionSetBuilder>()
            .TryAdd<IUpdateSqlGenerator>(p => p.GetService<IDynamicsUpdateSqlGenerator>())
            .TryAdd<IModificationCommandBatchFactory, DynamicsModificationCommandBatchFactory>()
            .TryAdd<IValueGeneratorSelector, DynamicsValueGeneratorSelector>()
            .TryAdd<IRelationalConnection>(p => p.GetService<IDynamicsConnection>())
            .TryAdd<IRelationalDatabaseCreator, DynamicsDatabaseCreator>()
            .TryAdd<IHistoryRepository, DynamicsHistoryRepository>()
            .TryAdd<ICompiledQueryCacheKeyGenerator, DynamicsCompiledQueryCacheKeyGenerator>()
            .TryAdd<IExecutionStrategyFactory, DynamicsExecutionStrategyFactory>()
            .TryAdd<ISingletonOptions, IDynamicsOptions>(p => p.GetService<IDynamicsOptions>())
            .TryAddCoreServices()

            .TryAdd<IMethodCallTranslatorProvider, DynamicsMethodCallTranslatorProvider>()
            .TryAdd<IMemberTranslatorProvider, DynamicsMemberTranslatorProvider>()
            .TryAdd<IQuerySqlGeneratorFactory, DynamicsQuerySqlGeneratorFactory>()
            .TryAdd<IQueryTranslationPostprocessorFactory, DynamicsQueryTranslationPostprocessorFactory>()
            .TryAdd<IRelationalSqlTranslatingExpressionVisitorFactory,
                DynamicsSqlTranslatingExpressionVisitorFactory>()
            .TryAddProviderSpecificServices(b => b
                .TryAddSingleton<IDynamicsValueGeneratorCache, DynamicsValueGeneratorCache>()
                .TryAddSingleton<IDynamicsOptions, DynamicsOptions>()
                .TryAddSingleton<IDynamicsUpdateSqlGenerator, DynamicsUpdateSqlGenerator>()
                .TryAddSingleton<IDynamicsSequenceValueGeneratorFactory, DynamicsSequenceValueGeneratorFactory>()
                .TryAddScoped<IDynamicsConnection, DynamicsConnection>())

            .TryAddCoreServices();

        // DynamicsCrmClient is scoped; it resolves IOrganizationServiceAsync2 from DI.
        // The consumer is responsible for registering IOrganizationServiceAsync2.
        services.TryAddScoped(sp =>
            new DynamicsCrmClient(sp.GetRequiredService<IOrganizationServiceAsync2>()));

        return services;
    }
}