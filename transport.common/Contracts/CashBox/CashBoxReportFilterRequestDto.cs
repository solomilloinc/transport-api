namespace Transport.SharedKernel.Contracts.CashBox;

public record CashBoxReportFilterRequestDto(
    DateTime? FromDate,
    DateTime? ToDate,
    string? Status
);
