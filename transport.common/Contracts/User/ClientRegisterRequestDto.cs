namespace Transport.SharedKernel.Contracts.User;

public sealed record ClientRegisterRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string DocumentNumber,
    string Phone1,
    string? Phone2,
    string Password,
    string? IpAddress);
