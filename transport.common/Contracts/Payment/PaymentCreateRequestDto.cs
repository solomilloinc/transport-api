namespace Transport.SharedKernel.Contracts.Payment;

public record PaymentCreateRequestDto(decimal TransactionAmount, 
    string Token, 
    string Description, 
    int Installments, 
    string PaymentMethodId,
    PayerInfo Payer);

public record PayerInfo(string Email);