namespace Transport.SharedKernel.Contracts.Reserve;

//ReservePayment
public record CreatePaymentRequestDto(decimal TransactionAmount,
    int PaymentMethod);
