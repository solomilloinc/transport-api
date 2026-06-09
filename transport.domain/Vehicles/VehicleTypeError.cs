using Transport.SharedKernel;

namespace Transport.Domain.Vehicles;

public static class VehicleTypeError
{

    public static readonly Error VehicleTypeNotFound = Error.NotFound(
        "VehicleType.NotFound",
        "El tipo de vehículo que estás buscando no existe"
    );

    public static readonly Error VehicleTypeAlreadyExists = Error.Validation(
        "VehicleType.AlreadyExists",
        "Ya existe un tipo de vehículo con el mismo nombre."
    );

    public static readonly Error InUse = Error.Conflict(
        "VehicleType.InUse",
        "No se puede eliminar un tipo de vehículo asignado a coches activos.");
}
