using System.Data;
using Microsoft.EntityFrameworkCore;

namespace transport.infraestructure.Database.Helpers;

public static class DbContextExtensions
{
    public static void RejectAllChanges(this ApplicationDbContext dbContext)
    {
        dbContext.ChangeTracker.Entries()
            .Where(e => e.Entity != null).ToList()
            .ForEach(e => e.State = EntityState.Detached);
    }

    public static void ExecuteInTransaction(this ApplicationDbContext dbContext, Action action)
    {
        dbContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using (var transaction = dbContext.Database.BeginTransaction())
            {
                try
                {
                    action();

                    dbContext.SaveChangesWithOutboxAsync();

                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        });
    }

    public static T ExecuteInTransaction<T>(this ApplicationDbContext dbContext, Func<T> action)
    {
        return dbContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using (var transaction = dbContext.Database.BeginTransaction())
            {
                try
                {
                    T result = action();

                    dbContext.SaveChangesWithOutboxAsync();

                    transaction.Commit();

                    return result;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        });
    }

    public static async Task ExecuteInTransactionAsync(this ApplicationDbContext dbContext,
      Func<Task> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel);
            try
            {
                await action();
                await dbContext.SaveChangesWithOutboxAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public static async Task<T> ExecuteInTransactionAsync<T>(this ApplicationDbContext dbContext,
        Func<Task<T>> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel);
            try
            {
                T result = await action();
                await dbContext.SaveChangesWithOutboxAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }


}
