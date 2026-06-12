namespace Transport.SharedKernel.Contracts.User;

public sealed record GoogleAuthenticatedUserDto(
    string Email,
    string FirstName,
    string LastName,
    string Subject,
    bool EmailVerified,
    string? PictureUrl,
    string? IpAddress);
