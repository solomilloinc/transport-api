using Transport.SharedKernel;

namespace Transport.Domain.Vehicles;

public static class VehicleTypeError
{

    public static readonly Error VehicleTypeNotFound = new(
        "VehicleType",
        "El Véhiculo no se encuentra Activo",
        ErrorType.Validation
    );
}
