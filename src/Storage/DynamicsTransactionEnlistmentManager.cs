using System;
using System.Transactions;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Storage;

public class DynamicsTransactionEnlistmentManager : ITransactionEnlistmentManager
{
    public void EnlistTransaction(Transaction? transaction) => throw new NotSupportedException();

    public Transaction? EnlistedTransaction => null;
}