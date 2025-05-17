namespace Transport.SharedKernel.Contracts.Customer;

public record CustomerReportResponseDto(
    int CustomerId,
    string FirstName,
    string LastName,
    string Email,
    string DocumentNumber,
    string Phone1,
    string? Phone2,
    DateTime CreatedDate);
