using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Storage;

public class DynamicsTransactionManager : IDbContextTransactionManager
{
    public void ResetState()
    {
        CurrentTransaction?.Dispose();
        CurrentTransaction = null;
    }
    
    public async Task ResetStateAsync(CancellationToken cancellationToken = new())
    {
        if (CurrentTransaction != null)
        {
            await CurrentTransaction.DisposeAsync().ConfigureAwait(false);
        }

        CurrentTransaction = null;
    }

    public IDbContextTransaction BeginTransaction()
    {
        EnsureNoTransactions();
        return CurrentTransaction = new DynamicsTransaction();
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = new())
    {
        EnsureNoTransactions();
        CurrentTransaction = new DynamicsTransaction();
        return Task.FromResult(CurrentTransaction);
    }

    public void CommitTransaction()
    {
        if (GetRequiredCurrentTransaction() is not { } acquiredTransaction) return;
        
        acquiredTransaction.Commit();
        acquiredTransaction.Dispose();
        CurrentTransaction = null;
    }

    public void RollbackTransaction()
    {
        if (GetRequiredCurrentTransaction() is not { } acquiredTransaction) return;
        
        acquiredTransaction.Rollback();
        acquiredTransaction.Dispose();
        CurrentTransaction = null;
    }
    
    private IDbContextTransaction GetRequiredCurrentTransaction()
        => CurrentTransaction
           ?? throw new InvalidOperationException("No transaction is in progress. Call BeginTransaction to start a transaction.");

    public IDbContextTransaction? CurrentTransaction { get; internal set; }
    
    private void EnsureNoTransactions()
    {
        if (CurrentTransaction != null)
        {
            throw new InvalidOperationException(
                "The connection is already in a transaction and cannot participate in another transaction.");
        }

        if (System.Transactions.Transaction.Current != null)
        {
            throw new InvalidOperationException(
                "An ambient transaction has been detected. The ambient transaction needs to be completed before starting a new transaction on this connection.");
        }
    }
}