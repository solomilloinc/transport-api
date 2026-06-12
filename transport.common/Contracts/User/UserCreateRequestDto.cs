namespace Transport.SharedKernel.Contracts.User;

public sealed record UserCreateRequestDto(
    string Email,
    string Password,
    string Role);
