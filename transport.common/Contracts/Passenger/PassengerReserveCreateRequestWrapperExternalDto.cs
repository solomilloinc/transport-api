using Transport.SharedKernel.Contracts.Customer;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.SharedKernel.Contracts.Passenger;

//Usuario
public record PassengerReserveCreateRequestWrapperExternalDto(CreatePaymentExternalRequestDto? Payment, List<PassengerReserveCreateRequestDto> Items);
