using System;
using System.Collections.Generic;
using System.Text;
using EfCore.Dynamics365.Client;
using EfCore.Dynamics365.Extensions;
using EfCore.Dynamics365.Query;
using EfCore.Dynamics365.Storage;
using EfCore.Dynamics365.ValueGeneration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Dynamics365.Infrastructure
{
    /// <summary>
    /// Registers the Dynamics 365 provider with the EF Core service container
    /// and stores the connection options.
    /// </summary>
    public sealed class DynamicsOptionsExtension : IDbContextOptionsExtension
    {
        private DynamicsClientOptions _clientOptions = new DynamicsClientOptions();
        private DbContextOptionsExtensionInfo? _info;

        public DynamicsOptionsExtension() { }

        private DynamicsOptionsExtension(DynamicsOptionsExtension src)
        {
            _clientOptions = src._clientOptions;
        }

        // ── Fluent copy-on-write helpers ─────────────────────────────────────

        internal DynamicsOptionsExtension WithServiceUrl(string url)
        {
            var clone = Clone();
            clone._clientOptions = CopyOptions(o => o.ServiceUrl = url);
            return clone;
        }

        internal DynamicsOptionsExtension WithTenantId(string id)
        {
            var clone = Clone();
            clone._clientOptions = CopyOptions(o => o.TenantId = id);
            return clone;
        }

        internal DynamicsOptionsExtension WithClientId(string id)
        {
            var clone = Clone();
            clone._clientOptions = CopyOptions(o => o.ClientId = id);
            return clone;
        }

        internal DynamicsOptionsExtension WithClientSecret(string secret)
        {
            var clone = Clone();
            clone._clientOptions = CopyOptions(o => o.ClientSecret = secret);
            return clone;
        }

        internal DynamicsOptionsExtension WithUsername(string username)
        {
            var clone = Clone();
            clone._clientOptions = CopyOptions(o => o.Username = username);
            return clone;
        }

        internal DynamicsOptionsExtension WithPassword(string password)
        {
            var clone = Clone();
            clone._clientOptions = CopyOptions(o => o.Password = password);
            return clone;
        }

        internal DynamicsOptionsExtension WithApiVersion(string version)
        {
            var clone = Clone();
            clone._clientOptions = CopyOptions(o => o.ApiVersion = version);
            return clone;
        }

        private DynamicsOptionsExtension Clone() => new DynamicsOptionsExtension(this);

        private DynamicsClientOptions CopyOptions(Action<DynamicsClientOptions> modify)
        {
            var opts = new DynamicsClientOptions
            {
                ServiceUrl   = _clientOptions.ServiceUrl,
                TenantId     = _clientOptions.TenantId,
                ClientId     = _clientOptions.ClientId,
                ClientSecret = _clientOptions.ClientSecret,
                Username     = _clientOptions.Username,
                Password     = _clientOptions.Password,
                ApiVersion   = _clientOptions.ApiVersion,
                HttpTimeout  = _clientOptions.HttpTimeout,
            };
            modify(opts);
            return opts;
        }

        public DynamicsClientOptions ClientOptions => _clientOptions;

        // ── IDbContextOptionsExtension ────────────────────────────────────────

        public DbContextOptionsExtensionInfo Info =>
            _info ??= new ExtensionInfo(this);

        public void ApplyServices(IServiceCollection services)
        {
            services.AddEntityFrameworkDynamics365();
        }

        public void Validate(IDbContextOptions options) { }

        // ── ExtensionInfo ─────────────────────────────────────────────────────

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(IDbContextOptionsExtension extension)
                : base(extension) { }

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
                debugInfo["Dynamics365:ApiVersion"] = ext._clientOptions.ApiVersion;
            }
        }
    }
}
