using Transport.SharedKernel;

namespace Transport.Domain.Customers;

public static class CustomerError
{
    public static readonly Error NotFound = Error.NotFound(
        "Customer.NotFound",
        "No se encontró el cliente con el ID especificado.");

    public static readonly Error AlreadyExists = Error.Conflict(
        "Customer.AlreadyExists",
        "Ya existe un cliente con ese número de documento.");

    public static readonly Error Inactive = Error.Validation(
        "Customer.Inactive",
        "El cliente no se encuentra activo.");
}
