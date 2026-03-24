using EfCore.Dynamics365.Query;
using EfCore.Dynamics365.Storage;
using EfCore.Dynamics365.ValueGeneration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EfCore.Dynamics365.Extensions
{
    /// <summary>
    /// Registers Dynamics 365 EF Core provider services into the DI container.
    /// Called automatically by <see cref="DynamicsOptionsExtension.ApplyServices"/>.
    /// </summary>
    public static class DynamicsServiceCollectionExtensions
    {
        public static IServiceCollection AddEntityFrameworkDynamics365(
            this IServiceCollection services)
        {
            var builder = new EntityFrameworkServicesBuilder(services)
                // Storage
                .TryAdd<IDatabase, DynamicsDatabase>()
                .TryAdd<IDatabaseCreator, DynamicsDatabaseCreator>()
                // Query pipeline
                .TryAdd<IQueryContextFactory, DynamicsQueryContextFactory>()
                .TryAdd<IQueryTranslationPreprocessorFactory,
                    DynamicsQueryTranslationPreprocessorFactory>()
                .TryAdd<IQueryableMethodTranslatingExpressionVisitorFactory,
                    DynamicsQueryableMethodTranslatingExpressionVisitorFactory>()
                .TryAdd<IShapedQueryCompilingExpressionVisitorFactory,
                    DynamicsShapedQueryCompilingExpressionVisitorFactory>()
                // Value generation
                .TryAdd<IValueGeneratorSelector, DynamicsValueGeneratorSelector>();

            builder.TryAddCoreServices();

            // The HTTP client is scoped so it shares a token cache per DbContext lifetime
            services.TryAddScoped(sp =>
            {
                var opts = sp.GetRequiredService<IDbContextOptions>()
                    .FindExtension<Infrastructure.DynamicsOptionsExtension>()!
                    .ClientOptions;
                return new Client.DynamicsHttpClient(opts);
            });

            return services;
        }
    }
}
