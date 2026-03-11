using Transport.SharedKernel;

namespace Transport.Domain.Trips;

public static class TripError
{
    public static readonly Error TripNotFound = Error.NotFound(
        "Trip.NotFound",
        "Trip not found");

    public static readonly Error TripAlreadyExists = Error.Conflict(
        "Trip.AlreadyExists",
        "A trip with this origin and destination already exists");

    public static readonly Error TripPriceNotFound = Error.NotFound(
        "TripPrice.NotFound",
        "Trip price not found");

    public static readonly Error TripPriceAlreadyExists = Error.Conflict(
        "TripPrice.AlreadyExists",
        "A price for this city and reserve type already exists in this trip");

    public static readonly Error InvalidTripConfiguration = Error.Validation(
        "Trip.InvalidConfiguration",
        "Origin and destination cities must be different");

    public static readonly Error TripNotActive = Error.Validation(
        "Trip.NotActive",
        "The trip is not active");

    public static readonly Error TripDirectionNotFound = Error.NotFound(
        "TripDirection.NotFound",
        "Trip direction not found");

    public static readonly Error TripDirectionAlreadyExists = Error.Conflict(
        "TripDirection.AlreadyExists",
        "A direction for this trip already exists");
}
