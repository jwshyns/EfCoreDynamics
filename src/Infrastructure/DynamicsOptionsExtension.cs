using System;
using System.Collections.Generic;
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
    private DbContextOptionsExtensionInfo? _info;
    
    public string? ConnectionString { get; private set; }

    public DynamicsOptionsExtension() { }

    private DynamicsOptionsExtension(DynamicsOptionsExtension src)
    {
    }

    private DynamicsOptionsExtension Clone() => new(this);
    
    public DynamicsOptionsExtension WithConnectionString(string connectionString)
    {
        var clone = Clone();
        clone.ConnectionString = connectionString;
        return clone;
    }
    

    // ── IDbContextOptionsExtension ────────────────────────────────────────

    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

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
                return $"ConnectionString={ext.ConnectionString} ";
            }
        }

        public override long GetServiceProviderHashCode() =>
            ((DynamicsOptionsExtension)Extension).ConnectionString?.GetHashCode() ?? 0L;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            var ext = (DynamicsOptionsExtension)Extension;
            debugInfo["Dynamics365:ConnectionString"] = ext.ConnectionString ?? "(null)";
        }
    }
}