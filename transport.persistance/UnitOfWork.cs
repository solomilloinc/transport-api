using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace transport.persistance;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext dbContext;

    public UnitOfWork(AppDbContext dbContext)
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

    public int SaveChanges()
    {
        return dbContext.SaveChanges();
    }

    public void RejectAllChanges()
    {
        dbContext.RejectAllChanges();
    }

}
