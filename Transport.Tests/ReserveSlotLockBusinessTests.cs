using FluentAssertions;
using Moq;
using System.Data;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Business.ReserveSlotLockBusiness;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Users;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;
using Xunit;

namespace Transport.Tests;

public class ReserveSlotLockBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserContext> _userContextMock;
    private readonly ReserveSlotLockBusiness _slotLockBusiness;

    public ReserveSlotLockBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userContextMock = new Mock<IUserContext>();

        _slotLockBusiness = new ReserveSlotLockBusiness(
            _contextMock.Object,
            _unitOfWorkMock.Object,
            _userContextMock.Object,
            new FakeReserveOption());
    }

    [Fact]
    public async Task AcquireAsync_ShouldSucceed_WhenValidRequest()
    {
        // Arrange
        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var reserves = new List<Reserve>
        {
            new Reserve
            {
                ReserveId = 1,
                Status = ReserveStatusEnum.Confirmed,
                Passengers = new List<Passenger>(),
                Vehicle = vehicle,
                Service = new Service { Vehicle = vehicle }
            }
        };

        var locks = new List<ReserveSlotLock>();
        var users = new List<User>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetMockDbSetWithIdentity(locks));
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(users));

        _userContextMock.Setup(x => x.Email).Returns("test@example.com");
        _userContextMock.Setup(x => x.UserId).Returns(1);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<LockReserveSlotsResponseDto>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<LockReserveSlotsResponseDto>>>, IsolationLevel>((func, _) => func());

        var request = new LockReserveSlotsRequestDto(1, null, 2);

        // Act
        var result = await _slotLockBusiness.AcquireAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LockToken.Should().NotBeNullOrEmpty();
        result.Value.TimeoutMinutes.Should().Be(10); // From FakeReserveOption
        locks.Should().HaveCount(1);
        locks[0].OutboundReserveId.Should().Be(1);
        locks[0].SlotsLocked.Should().Be(2);
        locks[0].Status.Should().Be(ReserveSlotLockStatus.Active);
        locks[0].UserEmail.Should().Be("test@example.com");
    }

    [Fact]
    public async Task AcquireAsync_ShouldFail_WhenInsufficientSlots()
    {
        // Arrange - Reserve already full
        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var reserves = new List<Reserve>
        {
            new Reserve
            {
                ReserveId = 1,
                Status = ReserveStatusEnum.Confirmed,
                Passengers = Enumerable.Range(0, 10)
                    .Select(_ => new Passenger { Status = PassengerStatusEnum.Confirmed })
                    .ToList(),
                Vehicle = vehicle,
                Service = new Service { Vehicle = vehicle }
            }
        };

        var locks = new List<ReserveSlotLock>();
        var users = new List<User>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks));
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(users));

        _userContextMock.Setup(x => x.Email).Returns("test@example.com");
        _userContextMock.Setup(x => x.UserId).Returns(1);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<LockReserveSlotsResponseDto>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<LockReserveSlotsResponseDto>>>, IsolationLevel>((func, _) => func());

        var request = new LockReserveSlotsRequestDto(1, null, 2);

        // Act
        var result = await _slotLockBusiness.AcquireAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveSlotLockError.InsufficientSlots);
        locks.Should().BeEmpty();
    }

    [Fact]
    public async Task AcquireAsync_ShouldFail_WhenMaxSimultaneousLocksExceeded()
    {
        // Arrange - User already has 5 active locks (FakeReserveOption.MaxSimultaneousLocksPerUser)
        var reserves = new List<Reserve>
        {
            new Reserve
            {
                ReserveId = 1,
                Status = ReserveStatusEnum.Confirmed,
                Passengers = new List<Passenger>(),
                Service = new Service { Vehicle = new Vehicle { AvailableQuantity = 10 } }
            }
        };

        var existingLocks = Enumerable.Range(1, 5).Select(i => new ReserveSlotLock
        {
            ReserveSlotLockId = i,
            UserEmail = "test@example.com",
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            OutboundReserveId = i + 10
        }).ToList();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(existingLocks));
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(new List<User>()));

        _userContextMock.Setup(x => x.Email).Returns("test@example.com");
        _userContextMock.Setup(x => x.UserId).Returns(1);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<LockReserveSlotsResponseDto>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<LockReserveSlotsResponseDto>>>, IsolationLevel>((func, _) => func());

        var request = new LockReserveSlotsRequestDto(1, null, 1);

        // Act
        var result = await _slotLockBusiness.AcquireAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveSlotLockError.MaxSimultaneousLocksExceeded);
    }

    [Fact]
    public async Task AcquireAsync_MultipleRequests_ShouldRespectAvailableSlots()
    {
        // Arrange - 10-capacity vehicle with 7 confirmed passengers (3 slots free)
        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var reserve = new Reserve
        {
            ReserveId = 1,
            Status = ReserveStatusEnum.Confirmed,
            Passengers = Enumerable.Range(0, 7)
                .Select(_ => new Passenger { Status = PassengerStatusEnum.Confirmed })
                .ToList(),
            Vehicle = vehicle,
            Service = new Service { Vehicle = vehicle }
        };
        var reserves = new List<Reserve> { reserve };
        var locks = new List<ReserveSlotLock>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetMockDbSetWithIdentity(locks));
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(new List<User>()));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<LockReserveSlotsResponseDto>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<LockReserveSlotsResponseDto>>>, IsolationLevel>((func, _) => func());

        // Act - First request takes 2 of 3 slots; second request needs 2 but only 1 is left
        var result1 = await _slotLockBusiness.AcquireAsync(new LockReserveSlotsRequestDto(1, null, 2));
        var result2 = await _slotLockBusiness.AcquireAsync(new LockReserveSlotsRequestDto(1, null, 2));

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsFailure.Should().BeTrue();
        locks.Should().HaveCount(1);
        locks[0].SlotsLocked.Should().Be(2);
        locks[0].Status.Should().Be(ReserveSlotLockStatus.Active);
        result1.Value.LockToken.Should().NotBeNullOrEmpty();
        result1.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task CancelAsync_ShouldSucceed_WhenLockActive()
    {
        // Arrange
        var lockToken = Guid.NewGuid().ToString();
        var activeLock = new ReserveSlotLock
        {
            ReserveSlotLockId = 1,
            LockToken = lockToken,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        var locks = new List<ReserveSlotLock> { activeLock };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Act
        var result = await _slotLockBusiness.CancelAsync(lockToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        activeLock.Status.Should().Be(ReserveSlotLockStatus.Cancelled);
        activeLock.UpdatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CancelAsync_ShouldFail_WhenLockNotFound()
    {
        // Arrange
        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(new List<ReserveSlotLock>()));

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Act
        var result = await _slotLockBusiness.CancelAsync("nonexistent-token");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveSlotLockError.LockNotFound);
    }

    [Fact]
    public async Task CleanupExpiredAsync_ShouldMarkExpiredLocksAsExpired()
    {
        // Arrange
        var activeLock = new ReserveSlotLock
        {
            ReserveSlotLockId = 1,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5) // Not expired
        };
        var expiredLock1 = new ReserveSlotLock
        {
            ReserveSlotLockId = 2,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var expiredLock2 = new ReserveSlotLock
        {
            ReserveSlotLockId = 3,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var locks = new List<ReserveSlotLock> { activeLock, expiredLock1, expiredLock2 };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Act
        var result = await _slotLockBusiness.CleanupExpiredAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        activeLock.Status.Should().Be(ReserveSlotLockStatus.Active);
        expiredLock1.Status.Should().Be(ReserveSlotLockStatus.Expired);
        expiredLock2.Status.Should().Be(ReserveSlotLockStatus.Expired);
    }

    [Fact]
    public async Task ValidateAsync_ShouldSucceed_WhenAllConditionsMet()
    {
        // Arrange
        var lockToken = Guid.NewGuid().ToString();
        var activeLock = new ReserveSlotLock
        {
            ReserveSlotLockId = 1,
            LockToken = lockToken,
            OutboundReserveId = 1,
            ReturnReserveId = 2,
            SlotsLocked = 3,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(new List<ReserveSlotLock> { activeLock }));

        // Act
        var result = await _slotLockBusiness.ValidateAsync(lockToken, 1, 2, 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(activeLock);
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenLockExpired()
    {
        // Arrange - lock expired so query (which filters by ExpiresAt > UtcNow) returns nothing
        var lockToken = Guid.NewGuid().ToString();
        var expiredLock = new ReserveSlotLock
        {
            ReserveSlotLockId = 1,
            LockToken = lockToken,
            OutboundReserveId = 1,
            SlotsLocked = 1,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(new List<ReserveSlotLock> { expiredLock }));

        // Act
        var result = await _slotLockBusiness.ValidateAsync(lockToken, 1, null, 1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveSlotLockError.InvalidOrExpiredLock);
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenPassengerCountMismatch()
    {
        // Arrange
        var lockToken = Guid.NewGuid().ToString();
        var activeLock = new ReserveSlotLock
        {
            ReserveSlotLockId = 1,
            LockToken = lockToken,
            OutboundReserveId = 1,
            SlotsLocked = 3, // 3 slots locked
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(new List<ReserveSlotLock> { activeLock }));

        // Act - request only 2 passengers though lock is for 3
        var result = await _slotLockBusiness.ValidateAsync(lockToken, 1, null, 2);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveSlotLockError.LockReserveMismatch);
    }

    [Fact]
    public async Task MarkAsUsedAsync_ShouldSetStatusToUsed()
    {
        // Arrange
        var activeLock = new ReserveSlotLock
        {
            ReserveSlotLockId = 1,
            LockToken = Guid.NewGuid().ToString(),
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(new List<ReserveSlotLock> { activeLock }));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Act
        var result = await _slotLockBusiness.MarkAsUsedAsync(activeLock);

        // Assert
        result.IsSuccess.Should().BeTrue();
        activeLock.Status.Should().Be(ReserveSlotLockStatus.Used);
        activeLock.UpdatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
