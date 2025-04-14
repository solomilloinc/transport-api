using Transport.SharedKernel;

namespace Transport.Domain.Customers;

public static class CustomerReserveError
{  
    public static readonly Error CustomerNotFound = Error.NotFound(
        "CustomerReserve.CustomerId",
        "El cliente con el ID especificado no existe");

    public static readonly Error ReserveNotFound = Error.Validation(
        "CustomerReserve.Reserve",
        "La reserva con el ID especificado no existe");

    public static readonly Error PickupLocationNotFound = Error.Validation(
        "CustomerReserve.PickupLocation",
        "El lugar de recogida es incorrecto");

    public static readonly Error DropoffLocationNotFound = Error.Validation(
        "CustomerReserve.DropoffLocation",
        "El lugar de Destino es incorrecto");
}
