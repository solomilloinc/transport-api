using Transport.SharedKernel;

namespace Transport.Domain.Vehicles;

public static class VehicleTypeError
{

    public static readonly Error VehicleTypeNotFound = new(
        "VehicleType",
        "El Véhiculo no se encuentra Activo",
        ErrorType.Validation
    );

    public static readonly Error InUse = Error.Conflict(
        "VehicleType.InUse",
        "No se puede eliminar un tipo de vehículo asignado a coches activos.");
}
