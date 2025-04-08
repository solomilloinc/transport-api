using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace Transport.Business.Data;

public interface IUnitOfWork
{
    IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted);
    void CommitTransaction();
    void ExecuteInTransaction(Action action);
    T ExecuteInTransaction<T>(Func<T> action);
    Task ExecuteInTransactionAsync(Func<Task> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
    IDbContextTransaction GetCurrentTransaction();
    void RejectAllChanges();
    void RollbackTransaction();
    Task<int> SaveChanges(CancellationToken cancellationToken = default);
}
