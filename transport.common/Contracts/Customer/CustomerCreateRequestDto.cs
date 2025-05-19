namespace Transport.SharedKernel.Contracts.Customer;

public record CustomerCreateRequestDto(string FirstName, 
    string LastName, 
    string Email, 
    string DocumentNumber, 
    string Phone1, 
    string? Phone2);
