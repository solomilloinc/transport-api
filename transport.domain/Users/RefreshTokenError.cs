using Transport.SharedKernel;

namespace Transport.Domain.Users;

public static class RefreshTokenError
{
    public static readonly Error RefreshNotFound = Error.Problem(
              "User.RefreshToken",
              "Invalid or expired refresh token");

    public static readonly Error TokenReused = Error.Problem(
              "User.RefreshToken.Reused",
              "Token has already been used. All sessions have been revoked for security.");

    public static readonly Error TokenExpired = Error.Problem(
              "User.RefreshToken.Expired",
              "Refresh token has expired");
}
