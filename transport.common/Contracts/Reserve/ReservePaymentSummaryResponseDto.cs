namespace Transport.SharedKernel.Contracts.Reserve;

public record ReservePaymentSummaryResponseDto(
    int ReserveId,
    List<PaymentMethodSummaryDto> PaymentsByMethod,
    decimal TotalAmount);

public record PaymentMethodSummaryDto(
    int PaymentMethodId,
    string PaymentMethodName,
    decimal Amount);
