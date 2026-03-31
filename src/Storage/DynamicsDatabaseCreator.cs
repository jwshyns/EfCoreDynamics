using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Storage;

public interface IDynamicsDatabaseCreator: IDatabaseCreator;

/// <summary>
/// Dataverse does not require schema creation — this implementation
/// is a no-op that always reports the "database" as existing.
/// </summary>
public sealed class DynamicsDatabaseCreator : IDynamicsDatabaseCreator
{
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