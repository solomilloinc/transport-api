using Microsoft.EntityFrameworkCore;
using Moq;
using System.Linq.Expressions;
using Transport.SharedKernel;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Transport.Domain.Tenants;
using Transport.Domain.Tenants.Abstraction;
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

    protected static DbSet<T> GetMockDbSetWithIdentity<T>(List<T> sourceList, Action<T>? onAdd = null) where T : class
    {
        return new TestDbSet<T>(sourceList, autoIdentity: true, onAdd: onAdd);
    }

    protected static DbSet<T> GetQueryableMockDbSet<T>(List<T> sourceList) where T : class
    {
        return new TestDbSet<T>(sourceList);
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

    public class FakeTenantContext : ITenantContext
    {
        public int TenantId { get; set; } = 1;
        public string? TenantCode { get; set; } = "default";
    }

    /// <summary>
    /// Builds a default mock for the tenant reserve config provider, returning
    /// the safe defaults (RoundTripSameDayOnly = true) so existing tests keep
    /// the historical IdaVuelta behavior when both legs are on the same day.
    /// </summary>
    protected static Mock<ITenantReserveConfigProvider> BuildTenantReserveConfigProviderMock(bool roundTripSameDayOnly = true)
    {
        var mock = new Mock<ITenantReserveConfigProvider>();
        mock.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantReserveConfig
            {
                TenantId = 1,
                RoundTripSameDayOnly = roundTripSameDayOnly
            });
        return mock;
    }
}
