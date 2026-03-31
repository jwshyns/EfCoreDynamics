using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Storage;

public class DynamicsTransaction : IDbContextTransaction
{
    public void Dispose()
    {
        // TODO release managed resources here
    }

    public async ValueTask DisposeAsync()
    {
        // TODO release managed resources here
    }

    public void Commit()
    {
        throw new NotImplementedException();
    }

    public void Rollback()
    {
        throw new NotImplementedException();
    }

    public Task CommitAsync(CancellationToken cancellationToken = new())
    {
        throw new NotImplementedException();
    }

    public Task RollbackAsync(CancellationToken cancellationToken = new())
    {
        throw new NotImplementedException();
    }

    public Guid TransactionId { get; } = Guid.NewGuid();
}