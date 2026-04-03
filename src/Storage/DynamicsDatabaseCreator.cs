using System;
using System.Threading;
using System.Threading.Tasks;
using EfCore.Dynamics365.Client;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Storage;

internal interface IDynamicsDatabaseCreator: IDatabaseCreator;

/// <summary>
/// Dataverse does not require schema creation — this implementation
/// is a no-op that always reports the "database" as existing.
/// </summary>
internal sealed class DynamicsDatabaseCreator : IDynamicsDatabaseCreator
{
    private IDynamicsClient _dynamicsClient;

    public DynamicsDatabaseCreator(IDynamicsClient dynamicsClient)
    {
        _dynamicsClient = dynamicsClient;
    }

    public bool EnsureCreated()
    {
        throw new NotSupportedException();
    }

    public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
    
    public bool EnsureDeleted()
    {
        throw new NotSupportedException();
    }

    public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
    
    public bool CanConnect()
    {
        // TODO: See what we can do here
        return true;
    }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        // TODO: See what we can do here
        return Task.FromResult(true);
    }
}