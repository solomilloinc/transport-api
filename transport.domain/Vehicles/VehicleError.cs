using Transport.SharedKernel;

namespace Transport.Domain.Vehicles;

public static class VehicleError
{
    public static readonly Error VehicleNotFound = new(
        "VehicleNotFound",
        "The vehicle you are looking for does not exist",
        ErrorType.NotFound
    );
    public static readonly Error VehicleAlreadyExists = new(
        "VehicleAlreadyExists",
        "The vehicle with the same internal number already exists.",
        ErrorType.Validation
    );

    public static readonly Error VehicleNotAvailable = new(
        "VehicleNotAvailable",
        "El Véhiculo no se encuentra Activo",
        ErrorType.Validation
    );

    public static readonly Error VehicleAvailableQuantityNotValid = new(
      "Vehicle.AvailableQuantity",
      "La cantidad de asientos disponibles no puede ser mayor a la cantidad del tipo de vehiculo",
      ErrorType.Validation
  );
}
