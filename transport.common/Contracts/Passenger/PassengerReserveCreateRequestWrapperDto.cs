using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.SharedKernel.Contracts.Passenger;

//Admin
public record PassengerReserveCreateRequestWrapperDto(List<CreatePaymentRequestDto> Payments, List<PassengerReserveCreateRequestDto> Items);
