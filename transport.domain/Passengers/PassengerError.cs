using Transport.SharedKernel;

namespace Transport.Domain.Passengers;

public static class PassengerError
{
    public static readonly Error NotFound = Error.NotFound(
        "Passenger.NotFound",
        "El Pasajero no fue encontrado.");

    public static Error NotActive(PassengerStatusEnum status) =>
        Error.Validation(
            "Passenger.NotActive",
            $"El pasajero no está activo (estado: {status}). Solo se pueden cancelar pasajeros pendientes de pago o confirmados.");

    public static readonly Error ReserveDeparted = Error.Validation(
        "Passenger.ReserveDeparted",
        "No se puede cancelar un pasajero cuya reserva ya partió.");
}
