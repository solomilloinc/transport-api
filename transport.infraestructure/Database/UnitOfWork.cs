using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using Transport.Business.Data;
using Transport.Infraestructure.Database.Helpers;
using Transport.SharedKernel;

namespace Transport.Infraestructure.Database;

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

    public IDbContextTransaction? GetCurrentTransaction()
    {
        return dbContext.Database.CurrentTransaction;
    }

    public Result ExecuteInTransaction(Func<Result> action)
    {
        return dbContext.ExecuteInTransaction(action);
    }

    public Result<T> ExecuteInTransaction<T>(Func<Result<T>> action)
    {
        return dbContext.ExecuteInTransaction(action);
    }

    public Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        return dbContext.ExecuteInTransactionAsync(action, isolationLevel);
    }

    public Task<Result<T>> ExecuteInTransactionAsync<T>(Func<Task<Result<T>>> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        return dbContext.ExecuteInTransactionAsync(action, isolationLevel);
    }

    public async Task<int> SaveChanges(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesWithOutboxAsync(cancellationToken);
    }

    public void RejectAllChanges()
    {
        dbContext.RejectAllChanges();
    }

    public Task ExecuteInTransactionAsync(Func<Task> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        return dbContext.ExecuteInTransactionAsync(action, isolationLevel);
    }
}