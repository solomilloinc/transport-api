using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

public static class ReserveError
{
    public static readonly Error NotFound = Error.NotFound(
        "Reserve.NotFound",
        "La reserva no fue encontrada.");

    public static readonly Error NotAvailable = Error.Conflict(
        "Reserve.NotAvailable",
        "La reserva no está disponible.");

    public static readonly Error PriceNotAvailable = Error.Problem(
        "Reserve.PriceNotAvailable",
        "No se encontró un precio válido para el tipo de reserva.");

    public static Error VehicleQuantityNotAvailable(int existing, int incoming, int capacity) =>
      Error.Problem(
          "Reserve.VehicleNotAvailable",
          $"No hay suficientes asientos disponibles. Ya reservados: {existing}, nuevos: {incoming}, capacidad: {capacity}."
      );

    public static Error CustomerAlreadyExists(string documentNumber) =>
        Error.Validation(
            "Reserve.CustomerAlreadyExists",
            $"El pasajero con documento {documentNumber} ya existe en la reserva.");
}
