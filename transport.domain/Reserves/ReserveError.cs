using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

public static class ReserveError
{
    public static readonly Error NotFound = Error.NotFound(
        "Reserve.NotFound",
        "La reserva no fue encontrada.");

    public static readonly Error NotAvailable = Error.NotFound(
        "Reserve.NotAvailable",
        "La reserva no está disponible.");

    public static readonly Error PriceNotAvailable = Error.Validation(
        "Reserve.PriceNotAvailable",
        "No se encontró un precio válido para el tipo de reserva.");

    public static Error VehicleQuantityNotAvailable(int existing, int incoming, int capacity) =>
        Error.Validation(
            "Reserve.VehicleNotAvailable",
            $"No hay suficientes asientos disponibles. Ya reservados: {existing}, nuevos: {incoming}, capacidad: {capacity}."
        );

    public static Error PassengerAlreadyExists(string documentNumber) =>
        Error.Validation(
            "Reserve.PassengerAlreadyExists",
            $"El pasajero con documento {documentNumber} ya existe en la reserva.");

    public static Error InvalidPaymentAmount(decimal expected, decimal provided) =>
        Error.Validation(
            "Reserve.InvalidPaymentAmount",
            $"El monto total pagado (${provided}) no coincide con el precio esperado (${expected})."
        );

    public static Error InvalidReserveCombination(string description) =>
        Error.Validation(
            "Reserve.InvalidReserveCombination",
            description
        );

    public static Error OverPaymentNotAllowed(decimal expected, decimal provided) =>
        Error.Validation(
            "Reserve.OverPaymentNotAllowed",
            $"El monto pagado (${provided}) supera el monto pendiente (${expected})."
        );

    public static Error AlreadyFullyPaid(int reserveId) =>
        Error.Validation(
            "Reserve.AlreadyFullyPaid",
            $"La reserva #{reserveId} ya está completamente pagada."
        );

    public static readonly Error NoDebtToSettle = Error.Validation(
        "Reserve.NoDebtToSettle",
        "No hay deuda pendiente en las reservas seleccionadas.");
}
