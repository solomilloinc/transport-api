namespace Transport.SharedKernel.Contracts.User;

public sealed record GoogleLoginRequestDto(string IdToken, string? IpAddress);
