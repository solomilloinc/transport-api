namespace Transport.SharedKernel.Contracts.Reserve;

public record ReservePriceReportResponseDto(int ReservePriceId,
    int ServiceId,
    string ServiceName,
    decimal Price,
    int ReserveTypeId);

public record ReservePaymentReportResponseDto(int ReservePaymentId,
    int ReserveId,
    int CustomerId,
    decimal TransactionAmount,
    int PaymentMethod);
