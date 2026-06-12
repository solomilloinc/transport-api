namespace Transport.SharedKernel.Contracts.User;

public sealed record ClientProfileCompleteRequestDto(
    string FirstName,
    string LastName,
    string DocumentNumber,
    string Phone1,
    string? Phone2);
