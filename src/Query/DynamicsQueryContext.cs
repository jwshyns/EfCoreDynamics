using System;
using EfCore.Dynamics365.Client;
using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query
{
    /// <summary>
    /// Per-query execution context. Carries the <see cref="DynamicsHttpClient"/>
    /// so compiled query delegates can access it at runtime.
    /// </summary>
    public sealed class DynamicsQueryContext : QueryContext
    {
        public DynamicsHttpClient Client { get; }

        public DynamicsQueryContext(
            QueryContextDependencies dependencies,
            DynamicsHttpClient client)
            : base(dependencies)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }
    }
}
