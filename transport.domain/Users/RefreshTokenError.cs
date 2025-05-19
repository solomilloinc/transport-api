using Transport.SharedKernel;

namespace Transport.Domain.Users;

public static class RefreshTokenError
{
    public static readonly Error RefreshNotFound = Error.Problem(
              "User.RefreshToken",
              "Invalid or expired refresh token");
}
