namespace Transport.SharedKernel.Contracts.Customer;

public record CustomerTransactionDto(
    int Id,
    int CustomerId,
    string? Description,
    string TransactionType,
    decimal Amount,
    DateTime Date
);

public record CustomerAccountSummaryDto(
    int CustomerId,
    string CustomerFullName,
    decimal CurrentBalance,
    decimal RangeTotalPagos,
    decimal RangeTotalCargos,
    PagedReportResponseDto<CustomerTransactionDto> Transactions
);
