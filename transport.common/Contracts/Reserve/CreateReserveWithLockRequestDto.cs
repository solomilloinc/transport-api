using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Customer;

namespace Transport.SharedKernel.Contracts.Reserve;

public record CreateReserveWithLockRequestDto(
    string LockToken,
    int ReserveTypeId,
    int OutboundReserveId,
    int? ReturnReserveId,
    List<PassengerBookingExternalDto> Passengers,
    CreatePaymentExternalRequestDto? Payment
);
