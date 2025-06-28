namespace Transport.SharedKernel.Contracts.Customer;
public record CustomerTransactionReportFilterRequestDto(
    int? TransactionType,
    DateTime? FromDate,
    DateTime? ToDate
);
