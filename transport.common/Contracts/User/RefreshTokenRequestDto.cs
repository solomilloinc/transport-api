namespace Transport.SharedKernel.Contracts.User;

public record RefreshTokenRequestDto(
    string RefreshToken,
    string IpAddress
);
