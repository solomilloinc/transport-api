using Transport.SharedKernel;

namespace Transport.Domain.Passengers;

public static class PassengerError
{
    public static readonly Error NotFound = Error.NotFound(
        "Passenger.NotFound",
        "El Pasajero no fue encontrado.");
}
