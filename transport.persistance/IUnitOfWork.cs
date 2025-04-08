using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace transport.persistance;

public interface IUnitOfWork
{
    IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted);
    void CommitTransaction();
    void ExecuteInTransaction(Action action);
    T ExecuteInTransaction<T>(Func<T> action);
    IDbContextTransaction GetCurrentTransaction();
    void RejectAllChanges();
    void RollbackTransaction();
    int SaveChanges();
}
