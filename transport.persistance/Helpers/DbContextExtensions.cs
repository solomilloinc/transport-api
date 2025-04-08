using Microsoft.EntityFrameworkCore;

namespace transport.persistance;

public static class DbContextExtensions
{
    public static void RejectAllChanges(this DbContext dbContext)
    {
        dbContext.ChangeTracker.Entries()
            .Where(e => e.Entity != null).ToList()
            .ForEach(e => e.State = EntityState.Detached);
    }

    public static void ExecuteInTransaction(this DbContext dbContext, Action action)
    {
        dbContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using (var transaction = dbContext.Database.BeginTransaction())
            {
                try
                {
                    action();

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

    public static T ExecuteInTransaction<T>(this DbContext dbContext, Func<T> action)
    {
        return dbContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using (var transaction = dbContext.Database.BeginTransaction())
            {
                try
                {
                    T result = action();

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

}
