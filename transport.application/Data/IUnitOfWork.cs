using System.Data;
using Microsoft.EntityFrameworkCore.Storage;
using Transport.SharedKernel;

namespace Transport.Business.Data;

public interface IUnitOfWork
{
    IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted);
    void CommitTransaction();
    void RollbackTransaction();
    IDbContextTransaction? GetCurrentTransaction();
    Result ExecuteInTransaction(Func<Result> action);
    Result<T> ExecuteInTransaction<T>(Func<Result<T>> action);
    Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
    Task<Result<T>> ExecuteInTransactionAsync<T>(Func<Task<Result<T>>> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);

    void RejectAllChanges();
    Task<int> SaveChanges(CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<Task> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
}