using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Reserves.Abstraction;

public interface IReserveSlotLockBusiness
{
    Task<Result<LockReserveSlotsResponseDto>> AcquireAsync(LockReserveSlotsRequestDto request);

    Task<Result<ReserveSlotLock>> ValidateAsync(
        string lockToken,
        int outboundReserveId,
        int? returnReserveId,
        int expectedPassengerCount);

    Task<Result<bool>> MarkAsUsedAsync(ReserveSlotLock slotLock);

    Task<Result<bool>> CancelAsync(string lockToken);

    Task<Result<bool>> CleanupExpiredAsync();
}
