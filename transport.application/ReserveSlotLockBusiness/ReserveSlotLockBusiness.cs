using Microsoft.EntityFrameworkCore;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Reserves.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Configuration;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveSlotLockBusiness;

public class ReserveSlotLockBusiness : IReserveSlotLockBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;
    private readonly IReserveOption _reserveOptions;

    public ReserveSlotLockBusiness(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IReserveOption reserveOptions)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _reserveOptions = reserveOptions;
    }

    public async Task<Result<LockReserveSlotsResponseDto>> AcquireAsync(LockReserveSlotsRequestDto request)
    {
        const int maxRetries = 3;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var userEmail = _userContext.Email;
                    var userId = _userContext.UserId;

                    Customer? associatedCustomer = null;
                    if (userId != null && userId > 0)
                    {
                        var user = await _context.Users
                            .Include(u => u.Customer)
                            .FirstOrDefaultAsync(u => u.UserId == userId);
                        associatedCustomer = user?.Customer;
                    }

                    var activeLocksCount = await _context.ReserveSlotLocks
                        .CountAsync(l => l.UserEmail == userEmail &&
                                       l.Status == ReserveSlotLockStatus.Active &&
                                       l.ExpiresAt > DateTime.UtcNow);

                    if (activeLocksCount >= _reserveOptions.MaxSimultaneousLocksPerUser)
                        return Result.Failure<LockReserveSlotsResponseDto>(ReserveSlotLockError.MaxSimultaneousLocksExceeded);

                    var availableSlots = await GetAvailableSlotsAsync(request.OutboundReserveId, request.ReturnReserveId);

                    if (availableSlots < request.PassengerCount)
                        return Result.Failure<LockReserveSlotsResponseDto>(ReserveSlotLockError.InsufficientSlots);

                    var lockToken = Guid.NewGuid().ToString();
                    var expiresAt = DateTime.UtcNow.AddMinutes(_reserveOptions.SlotLockTimeoutMinutes);

                    var slotLock = new ReserveSlotLock
                    {
                        LockToken = lockToken,
                        OutboundReserveId = request.OutboundReserveId,
                        ReturnReserveId = request.ReturnReserveId,
                        SlotsLocked = request.PassengerCount,
                        ExpiresAt = expiresAt,
                        Status = ReserveSlotLockStatus.Active,
                        UserEmail = userEmail,
                        UserDocumentNumber = associatedCustomer?.DocumentNumber,
                        CustomerId = associatedCustomer?.CustomerId,
                        RowVersion = new byte[8]
                    };

                    _context.ReserveSlotLocks.Add(slotLock);
                    await _context.SaveChangesWithOutboxAsync();

                    return Result.Success(new LockReserveSlotsResponseDto(
                        lockToken,
                        expiresAt,
                        _reserveOptions.SlotLockTimeoutMinutes
                    ));
                });
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(Random.Shared.Next(10, 100));
                continue;
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("deadlock") == true && attempt < maxRetries - 1)
            {
                await Task.Delay(Random.Shared.Next(50, 200));
                continue;
            }
        }

        return Result.Failure<LockReserveSlotsResponseDto>(
            Error.Failure("ConcurrencyConflict", "Unable to acquire slot lock due to high concurrency. Please try again."));
    }

    public async Task<Result<ReserveSlotLock>> ValidateAsync(
        string lockToken,
        int outboundReserveId,
        int? returnReserveId,
        int expectedPassengerCount)
    {
        var slotLock = await _context.ReserveSlotLocks
            .FirstOrDefaultAsync(l => l.LockToken == lockToken &&
                                     l.Status == ReserveSlotLockStatus.Active &&
                                     l.ExpiresAt > DateTime.UtcNow);

        if (slotLock == null)
            return Result.Failure<ReserveSlotLock>(ReserveSlotLockError.InvalidOrExpiredLock);

        if (slotLock.OutboundReserveId != outboundReserveId ||
            slotLock.ReturnReserveId != returnReserveId)
            return Result.Failure<ReserveSlotLock>(ReserveSlotLockError.LockReserveMismatch);

        if (expectedPassengerCount != slotLock.SlotsLocked)
            return Result.Failure<ReserveSlotLock>(ReserveSlotLockError.LockReserveMismatch);

        return Result.Success(slotLock);
    }

    public async Task<Result<bool>> MarkAsUsedAsync(ReserveSlotLock slotLock)
    {
        slotLock.Status = ReserveSlotLockStatus.Used;
        slotLock.UpdatedDate = DateTime.UtcNow;
        _context.ReserveSlotLocks.Update(slotLock);
        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> CancelAsync(string lockToken)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var slotLock = await _context.ReserveSlotLocks
                .FirstOrDefaultAsync(l => l.LockToken == lockToken &&
                                         l.Status == ReserveSlotLockStatus.Active);

            if (slotLock == null)
                return Result.Failure<bool>(ReserveSlotLockError.LockNotFound);

            slotLock.Status = ReserveSlotLockStatus.Cancelled;
            slotLock.UpdatedDate = DateTime.UtcNow;

            _context.ReserveSlotLocks.Update(slotLock);
            await _context.SaveChangesWithOutboxAsync();

            return Result.Success(true);
        });
    }

    public async Task<Result<bool>> CleanupExpiredAsync()
    {
        var expiredLocks = await _context.ReserveSlotLocks
            .Where(l => l.Status == ReserveSlotLockStatus.Active &&
                       l.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        if (expiredLocks.Any())
        {
            foreach (var expiredLock in expiredLocks)
            {
                expiredLock.Status = ReserveSlotLockStatus.Expired;
                expiredLock.UpdatedDate = DateTime.UtcNow;
            }

            _context.ReserveSlotLocks.UpdateRange(expiredLocks);
            await _context.SaveChangesWithOutboxAsync();
        }

        return Result.Success(true);
    }

    private async Task<int> GetAvailableSlotsAsync(int outboundReserveId, int? returnReserveId = null)
    {
        var reserveIds = new List<int> { outboundReserveId };
        if (returnReserveId.HasValue) reserveIds.Add(returnReserveId.Value);

        var reserves = await _context.Reserves
            .Include(r => r.Passengers)
            .Include(r => r.Vehicle)
            .Where(r => reserveIds.Contains(r.ReserveId))
            .ToListAsync();

        var minAvailable = int.MaxValue;

        foreach (var reserve in reserves)
        {
            var confirmedPassengers = reserve.Passengers
                .Count(p => p.Status == PassengerStatusEnum.Confirmed ||
                           p.Status == PassengerStatusEnum.PendingPayment);

            var activeLocks = await _context.ReserveSlotLocks
                .Where(l => (l.OutboundReserveId == reserve.ReserveId || l.ReturnReserveId == reserve.ReserveId) &&
                           l.Status == ReserveSlotLockStatus.Active &&
                           l.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            var totalActiveLocks = activeLocks.Sum(l => l.SlotsLocked);

            var available = reserve.Vehicle.AvailableQuantity - confirmedPassengers - totalActiveLocks;
            minAvailable = Math.Min(minAvailable, available);

            reserve.UpdatedDate = DateTime.UtcNow;
            reserve.UpdatedBy = "SlotLockSystem";
        }

        return Math.Max(0, minAvailable);
    }
}
