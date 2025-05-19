namespace Transport.SharedKernel.Contracts.Customer;

public record CustomerUpdateRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone1,
    string? Phone2);
