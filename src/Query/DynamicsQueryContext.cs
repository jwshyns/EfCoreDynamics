using System;
using EfCore.Dynamics365.Client;
using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query;

/// <summary>
/// Per-query execution context. Carries the <see cref="IDynamicsClient"/>
/// so compiled query delegates can access it at runtime.
/// </summary>
internal sealed class DynamicsQueryContext : QueryContext
{
    public IDynamicsClient Client { get; }

    public DynamicsQueryContext(
        QueryContextDependencies dependencies,
        IDynamicsClient client)
        : base(dependencies)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }
}