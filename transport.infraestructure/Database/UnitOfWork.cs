using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Threading;
using transport.infraestructure.Database.Helpers;
using Transport.Business.Data;

namespace transport.infraestructure.Database;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext dbContext;

    public UnitOfWork(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted)
    {
        return dbContext.Database.BeginTransaction(isolationLevel);
    }

    public void CommitTransaction()
    {
        dbContext.Database.CommitTransaction();
    }

    public void RollbackTransaction()
    {
        dbContext.Database.RollbackTransaction();
    }

    public IDbContextTransaction GetCurrentTransaction()
    {
        return dbContext.Database.CurrentTransaction;
    }

    public void ExecuteInTransaction(Action action)
    {
        dbContext.ExecuteInTransaction(action);
    }

    public T ExecuteInTransaction<T>(Func<T> action)
    {
        return dbContext.ExecuteInTransaction<T>(action);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        await dbContext.ExecuteInTransactionAsync(action, isolationLevel);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        return await dbContext.ExecuteInTransactionAsync(action, isolationLevel);
    }

    public async Task<int> SaveChanges(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesWithOutboxAsync(cancellationToken);
    }

    public void RejectAllChanges()
    {
        dbContext.RejectAllChanges();
    }

}
