using System.Data;
using Microsoft.EntityFrameworkCore;
using Transport.SharedKernel;

namespace Transport.Infraestructure.Database.Helpers;

public static class DbContextExtensions
{
    public static void RejectAllChanges(this ApplicationDbContext dbContext)
    {
        dbContext.ChangeTracker.Entries()
            .Where(e => e.Entity != null)
            .ToList()
            .ForEach(e => e.State = EntityState.Detached);
    }

    public static Result ExecuteInTransaction(this ApplicationDbContext dbContext, Func<Result> action)
    {
        return dbContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                var result = action();

                if (result.IsFailure)
                {
                    transaction.Rollback();
                    dbContext.RejectAllChanges();
                    return result;
                }

                dbContext.SaveChangesWithOutboxAsync().GetAwaiter().GetResult();
                transaction.Commit();

                return result;
            }
            catch
            {
                transaction.Rollback();
                dbContext.RejectAllChanges();
                throw;
            }
        });
    }

    public static Result<T> ExecuteInTransaction<T>(this ApplicationDbContext dbContext, Func<Result<T>> action)
    {
        return dbContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                var result = action();

                if (result.IsFailure)
                {
                    transaction.Rollback();
                    dbContext.RejectAllChanges();
                    return result;
                }

                dbContext.SaveChangesWithOutboxAsync().GetAwaiter().GetResult();
                transaction.Commit();

                return result;
            }
            catch
            {
                transaction.Rollback();
                dbContext.RejectAllChanges();
                throw;
            }
        });
    }

    public static async Task<Result> ExecuteInTransactionAsync(this ApplicationDbContext dbContext,
        Func<Task<Result>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel);
            try
            {
                var result = await action();

                if (result.IsFailure)
                {
                    await transaction.RollbackAsync();
                    dbContext.RejectAllChanges();
                    return result;
                }

                await dbContext.SaveChangesWithOutboxAsync();
                await transaction.CommitAsync();

                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                dbContext.RejectAllChanges();
                throw;
            }
        });
    }

    public static async Task<Result<T>> ExecuteInTransactionAsync<T>(this ApplicationDbContext dbContext,
        Func<Task<Result<T>>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel);
            try
            {
                var result = await action();

                if (result.IsFailure)
                {
                    await transaction.RollbackAsync();
                    dbContext.RejectAllChanges();
                    return result;
                }

                await dbContext.SaveChangesWithOutboxAsync();
                await transaction.CommitAsync();

                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                dbContext.RejectAllChanges();
                throw;
            }
        });
    }

    public static async Task ExecuteInTransactionAsync(this ApplicationDbContext dbContext,
    Func<Task> action,
    IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
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
                dbContext.RejectAllChanges();
                throw;
            }
        });
    }
}
