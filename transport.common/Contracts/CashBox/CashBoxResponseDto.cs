using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.SharedKernel.Contracts.CashBox;

public record CashBoxResponseDto(
    int CashBoxId,
    string? Description,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string Status,
    string OpenedByUserEmail,
    string? ClosedByUserEmail,
    int? ReserveId,
    int TotalPayments,
    decimal TotalAmount,
    List<PaymentMethodSummaryDto> PaymentsByMethod
);
