namespace Transport.SharedKernel.Contracts.Customer;

//Mercado Pago
public record CreatePaymentExternalRequestDto(decimal TransactionAmount,
    string Token,
    string Description,
    int Installments,
    string PaymentMethodId,
    string PayerEmail,
    string IdentificationType,
    string IdentificationNumber,
    int ReserveTypeId);
