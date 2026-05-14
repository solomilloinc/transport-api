using Transport.SharedKernel.Contracts.Customer;

namespace Transport.SharedKernel.Contracts.Passenger;

//Usuario
public record PassengerReserveCreateRequestWrapperExternalDto(
    int ReserveTypeId,
    int OutboundReserveId,
    int? ReturnReserveId,
    CreatePaymentExternalRequestDto? Payment,
    List<PassengerBookingExternalDto> Passengers);
