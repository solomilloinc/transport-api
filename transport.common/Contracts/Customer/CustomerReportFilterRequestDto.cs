namespace Transport.SharedKernel.Contracts.Customer;

public record CustomerReportFilterRequestDto(
    string? FirstName,
    string? LastName,
    string? Email,
    string? DocumentNumber,
    string? Phone1,
    string? Phone2,
    DateTime? CreatedFrom,
    DateTime? CreatedTo);