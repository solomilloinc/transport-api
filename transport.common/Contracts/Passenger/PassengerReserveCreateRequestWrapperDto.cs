using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.SharedKernel.Contracts.Passenger;

//Admin
public record PassengerReserveCreateRequestWrapperDto(
    int ReserveTypeId,
    int OutboundReserveId,
    int? ReturnReserveId,
    List<CreatePaymentRequestDto> Payments,
    List<PassengerBookingDto> Passengers);
