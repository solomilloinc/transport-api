using Transport.SharedKernel;

namespace Transport.Domain.Customers;

public static class CustomerError
{
    public static readonly Error CustomerNotFound = Error.Validation(
       "Customer.CustomerId",
       "El cliente no existe");

    public static readonly Error CustomerAlreadyExist = Error.Validation(
        "Customer.Document",
        "Hay un cliente que ya éxiste con este documento");
}
