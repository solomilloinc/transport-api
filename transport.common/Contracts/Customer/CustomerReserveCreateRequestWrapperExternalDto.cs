using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.SharedKernel.Contracts.Customer;

//Usuario
public record CustomerReserveCreateRequestWrapperExternalDto(CreatePaymentExternalRequestDto payment, List<CustomerReserveCreateRequestDto> Items);
