using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Storage;

/// <summary>
/// Dataverse does not require schema creation — this implementation
/// is a no-op that always reports the "database" as existing.
/// </summary>
public sealed class DynamicsDatabaseCreator : IRelationalDatabaseCreator
{
    /// <summary>
    /// Dataverse environments cannot be created via the Web API.
    /// This method always returns <c>false</c>.
    /// </summary>
    public bool EnsureCreated()
    {
        throw new System.NotImplementedException();
    }

    public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Dataverse environments cannot be deleted via the Web API.
    /// This method always returns <c>false</c>.
    /// </summary>
    public bool EnsureDeleted()
    {
        throw new System.NotImplementedException();
    }

    public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Returns <c>true</c> — the Dataverse environment is presumed to exist
    /// if a valid ServiceUrl is configured.
    /// </summary>
    public bool CanConnect()
    {
        throw new System.NotImplementedException();
    }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }

    public bool Exists()
    {
        throw new System.NotImplementedException();
    }

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public bool HasTables()
    {
        throw new System.NotImplementedException();
    }

    public Task<bool> HasTablesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public void Create()
    {
        throw new System.NotImplementedException();
    }

    public Task CreateAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public void Delete()
    {
        throw new System.NotImplementedException();
    }

    public Task DeleteAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public void CreateTables()
    {
        throw new System.NotImplementedException();
    }

    public Task CreateTablesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public string GenerateCreateScript()
    {
        throw new System.NotImplementedException();
    }
}