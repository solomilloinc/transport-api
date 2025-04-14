using Transport.SharedKernel;

namespace Transport.Domain.Users;

public static class LoginError
{
    public static readonly Error DriverNotFound = Error.Problem(
          "User.Login",
          "El chofer no existe");
}
