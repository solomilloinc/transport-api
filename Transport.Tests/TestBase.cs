using Microsoft.EntityFrameworkCore;
using Moq;
using System.Linq.Expressions;
using Transport.SharedKernel;
using Transport.Business.Data;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Transport.SharedKernel.Configuration;

namespace Transport.Tests;

public abstract class TestBase
{
    protected readonly Mock<IApplicationDbContext> ContextMock;
    protected readonly Mock<IUnitOfWork> UnitOfWorkMock;

    protected TestBase()
    {
        ContextMock = new Mock<IApplicationDbContext>();
        UnitOfWorkMock = new Mock<IUnitOfWork>();
    }

    protected Mock<DbSet<T>> GetMockDbSetWithIdentity<T>(List<T> sourceList, Action<T>? onAdd = null) where T : class
    {
        var queryable = sourceList.AsQueryable();

        var asyncEnumerable = new TestAsyncEnumerable<T>(queryable);
        var mockSet = new Mock<DbSet<T>>();

        mockSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(asyncEnumerable.GetAsyncEnumerator);

        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator);

        mockSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(entity =>
        {
            var idPropertyName = typeof(T).Name + "Id";
            var idProp = typeof(T).GetProperty(idPropertyName);
            if (idProp != null && idProp.PropertyType == typeof(int))
            {
                int newId = sourceList.Count + 1;
                idProp.SetValue(entity, newId);
            }

            sourceList.Add(entity);
            onAdd?.Invoke(entity);
        });

        return mockSet;
    }

    protected static Mock<DbSet<T>> GetQueryableMockDbSet<T>(List<T> sourceList) where T : class
    {
        var queryable = sourceList.AsQueryable();
        var asyncEnumerable = new TestAsyncEnumerable<T>(queryable);

        var mockSet = new Mock<DbSet<T>>();

        mockSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(asyncEnumerable.GetAsyncEnumerator);

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Expression)
            .Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>()
            .Setup(m => m.ElementType)
            .Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>()
            .Setup(m => m.GetEnumerator())
            .Returns(queryable.GetEnumerator);

        mockSet.Setup(m => m.FindAsync(It.IsAny<object[]>()))
            .ReturnsAsync((object[] ids) =>
            {
                if (ids == null || ids.Length == 0) return null;

                var id = ids[0];
                var pkProperty = typeof(T).GetProperties()
                    .FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                                       p.Name.Equals($"{typeof(T).Name}Id", StringComparison.OrdinalIgnoreCase));

                if (pkProperty == null) return null;

                return sourceList.FirstOrDefault(e => id.Equals(pkProperty.GetValue(e)));
            });

        mockSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(sourceList.Add);
        mockSet.Setup(d => d.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((T entity, CancellationToken _) =>
               {
                   sourceList.Add(entity);
                   return null!;
               });

        return mockSet;
    }


    protected void SetupSaveChangesWithOutboxAsync(Mock<IApplicationDbContext> contextMock)
    {
        contextMock.Setup(x => x.SaveChangesWithOutboxAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(1);
    }

    protected TDomainEvent? GetRaisedEvent<TEntity, TDomainEvent>(TEntity entity)
        where TEntity : Entity
        where TDomainEvent : class, IDomainEvent
    {
        return entity.DomainEvents.OfType<TDomainEvent>().FirstOrDefault();
    }

    public class FakeReserveOption : IReserveOption
    {
        public int ReserveGenerationDays { get; set; } = 15;
        public int SlotLockTimeoutMinutes { get; set; } = 10;
        public int SlotLockCleanupIntervalMinutes { get; set; } = 1;
        public int MaxSimultaneousLocksPerUser { get; set; } = 5;
    }

}
