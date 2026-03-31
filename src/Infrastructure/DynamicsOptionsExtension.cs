using System.Collections.Generic;
using EfCore.Dynamics365.Client;
using EfCore.Dynamics365.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Dynamics365.Infrastructure;

/// <summary>
/// Registers the Dynamics 365 provider with the EF Core service container
/// and stores minimal connection metadata.
/// </summary>
public sealed class DynamicsOptionsExtension : IDbContextOptionsExtension
{
    private DynamicsClientOptions _clientOptions = new();
    private DbContextOptionsExtensionInfo? _info;

    public DynamicsOptionsExtension() { }

    private DynamicsOptionsExtension(DynamicsOptionsExtension src)
    {
        _clientOptions = src._clientOptions;
    }

    internal DynamicsOptionsExtension WithServiceUrl(string url)
    {
        return new DynamicsOptionsExtension(this)
        {
            _clientOptions = new DynamicsClientOptions { ServiceUrl = url }
        };
    }

    public DynamicsClientOptions ClientOptions => _clientOptions;

    // ── IDbContextOptionsExtension ────────────────────────────────────────

    public DbContextOptionsExtensionInfo Info =>
        _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services) => services.AddEntityFrameworkDynamics365();

    public void Validate(IDbContextOptions options) { }

    // ── ExtensionInfo ─────────────────────────────────────────────────────

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        public ExtensionInfo(IDbContextOptionsExtension extension) : base(extension) { }

        public override bool IsDatabaseProvider => true;

        public override string LogFragment
        {
            get
            {
                var ext = (DynamicsOptionsExtension)Extension;
                return $"ServiceUrl={ext._clientOptions.ServiceUrl} ";
            }
        }

        public override long GetServiceProviderHashCode() =>
            ((DynamicsOptionsExtension)Extension)._clientOptions.ServiceUrl?.GetHashCode() ?? 0L;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            var ext = (DynamicsOptionsExtension)Extension;
            debugInfo["Dynamics365:ServiceUrl"] = ext._clientOptions.ServiceUrl ?? "(null)";
        }
    }
}