using transport.common;

namespace transport.domain.Drivers;

public static class DriverError
{
    //TODO. Acá podrías poner NotFound, Problem, Failure, Conflict etc.

    public static readonly Error EmailInBlackList = Error.Validation(
        "Driver.DocumentInvalid",
        "Este Documento no está permitido");
}
