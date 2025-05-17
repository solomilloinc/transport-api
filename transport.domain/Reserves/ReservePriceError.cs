using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

public static class ReservePriceError
{
    public static readonly Error ReservePriceNotFound = new(
            "ReservePriceNotFound",
            "El Servicio o el Precio no existen o está mal configurado",
            ErrorType.NotFound
        );

    public static readonly Error ReservePriceAlreadyExists = new(
        "ReservePriceAlreadyExists",
        "El Precio que se intenta dar de alta ya éxiste en el Servicio",
        ErrorType.Validation
    );
}
