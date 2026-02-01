namespace Transport.SharedKernel.Contracts.Customer;

public record CustomerServiceDto(int ServiceId, string ServiceName);

public record CustomerReportResponseDto(
    int CustomerId,
    string FirstName,
    string LastName,
    string Email,
    string DocumentNumber,
    string Phone1,
    string? Phone2,
    DateTime CreatedDate,
    decimal CurrentBalance,
    List<CustomerServiceDto> Services);
