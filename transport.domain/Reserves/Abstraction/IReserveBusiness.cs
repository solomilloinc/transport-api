using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Reserves.Abstraction;

public interface IReserveBusiness
{
    Task<Result<int>> CreateReserve(ReserveCreateDto dto);
    Task<Result<bool>> CreatePassengerReserves(PassengerReserveCreateRequestWrapperDto dto);

    Task<Result<bool>> UpdateReserveAsync(int reserveId, ReserveUpdateRequestDto request);

    Task<Result<bool>> UpdatePassengerReserveAsync(int customerReserveId, PassengerReserveUpdateRequestDto request);
    Task<Result<List<CustomerPendingReserveDto>>> GetCustomerPendingReservesAsync(int customerId);

    Task<Result<CreateReserveExternalResult>> CreatePassengerReservesWithLock(CreateReserveWithLockRequestDto request);
}
