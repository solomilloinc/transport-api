using Transport.SharedKernel;

namespace Transport.Domain.Services;

public static class ServiceError
{
    public static readonly Error ServiceNotFound = new(
            "VehicleNotFound",
            "The vehicle you are looking for does not exist",
            ErrorType.NotFound
        );
}
