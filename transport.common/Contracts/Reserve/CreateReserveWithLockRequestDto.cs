using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Customer;

namespace Transport.SharedKernel.Contracts.Reserve;

public record CreateReserveWithLockRequestDto(
    string LockToken,
    List<PassengerReserveExternalCreateRequestDto> Items,
    CreatePaymentExternalRequestDto? Payment
);