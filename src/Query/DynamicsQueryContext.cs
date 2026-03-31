using System;
using EfCore.Dynamics365.Client;
using Microsoft.EntityFrameworkCore.Query;

namespace EfCore.Dynamics365.Query;

/// <summary>
/// Per-query execution context. Carries the <see cref="DynamicsCrmClient"/>
/// so compiled query delegates can access it at runtime.
/// </summary>
public sealed class DynamicsQueryContext : QueryContext
{
    public DynamicsCrmClient Client { get; }

    public DynamicsQueryContext(
        QueryContextDependencies dependencies,
        DynamicsCrmClient client)
        : base(dependencies)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }
}