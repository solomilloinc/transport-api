﻿using Transport.SharedKernel;

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
}
