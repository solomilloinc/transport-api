using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Reserves.Abstraction;

public interface IReserveBusiness
{
    Task<Result<bool>> CreatePassengerReserves(int reserveId,
    int reserveTypeId,
    List<CustomerReserveCreateRequestDto> passengers);
}