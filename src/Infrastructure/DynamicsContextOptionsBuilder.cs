using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EfCore.Dynamics365.Infrastructure
{
    /// <summary>
    /// Fluent builder for Dynamics 365 provider-specific options.
    /// Obtained via <see cref="DynamicsDbContextOptionsExtensions.UseDynamics365"/>.
    /// </summary>
    public sealed class DynamicsDbOptionsBuilder
    {
        private readonly DbContextOptionsBuilder _optionsBuilder;

        internal DynamicsDbOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
        {
            _optionsBuilder = optionsBuilder;
        }

        /// <summary>
        /// Sets the Dataverse Web API version (default: "v9.2").
        /// </summary>
        public DynamicsDbOptionsBuilder UseApiVersion(string version)
        {
            var ext = GetOrCreateExtension().WithApiVersion(version);
            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder).AddOrUpdateExtension(ext);
            return this;
        }

        private DynamicsOptionsExtension GetOrCreateExtension()
        {
            var ext = _optionsBuilder.Options.FindExtension<DynamicsOptionsExtension>();
            return ext ?? new DynamicsOptionsExtension();
        }
    }
}
