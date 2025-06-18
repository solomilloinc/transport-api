using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.SharedKernel.Contracts.Customer;

//Admin
public record CustomerReserveCreateRequestWrapperDto(List<CreatePaymentRequestDto> Payments, List<CustomerReserveCreateRequestDto> Items);

//ReservePayment
public record CreatePaymentRequestDto(decimal TransactionAmount,
    int PaymentMethod);

public record CustomerCreateRequestDto(string FirstName,
    string LastName,
    string Email,
    string DocumentNumber,
    string Phone1,
    string? Phone2);
