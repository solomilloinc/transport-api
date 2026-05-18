using Transport.SharedKernel;

namespace Transport.Domain.FrequentSubscriptions;

public static class FrequentSubscriptionError
{
    public static readonly Error NotFound = Error.NotFound(
        "FrequentSubscription.NotFound",
        "No se encontró la suscripción frecuente.");

    public static readonly Error InvalidIdaConfig = Error.Validation(
        "FrequentSubscription.InvalidIdaConfig",
        "Para una suscripción Ida, no se debe especificar Inbound Service ni datos de vuelta.");

    public static readonly Error InvalidIdaVueltaConfig = Error.Validation(
        "FrequentSubscription.InvalidIdaVueltaConfig",
        "Para una suscripción IdaVuelta se requieren Inbound Service y pickup/dropoff del leg de vuelta.");

    public static readonly Error InvalidDateRange = Error.Validation(
        "FrequentSubscription.InvalidDateRange",
        "EndDate debe ser igual o posterior a StartDate.");

    public static readonly Error CannotChangeImmutableFields = Error.Validation(
        "FrequentSubscription.CannotChangeImmutableFields",
        "Customer, Services y ReserveType no se pueden modificar. Cancelá la suscripción y creá una nueva.");

    public static readonly Error CannotChangeStartDateAlreadyStarted = Error.Validation(
        "FrequentSubscription.CannotChangeStartDateAlreadyStarted",
        "No se puede modificar StartDate de una suscripción que ya comenzó.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "FrequentSubscription.AlreadyCancelled",
        "La suscripción ya no está activa.");

    public static Error DirectionNotAllowedForService(int serviceId, int directionId, SubscriptionLeg leg, DirectionKind kind) =>
        Error.Validation(
            "FrequentSubscription.DirectionNotAllowedForService",
            $"La direction {directionId} no está habilitada en el service {serviceId}.")
            .WithDetails(new Dictionary<string, object>
            {
                ["leg"] = leg == SubscriptionLeg.Outbound ? "outbound" : "inbound",
                ["kind"] = kind == DirectionKind.Pickup ? "pickup" : "dropoff",
                ["serviceId"] = serviceId,
                ["directionId"] = directionId
            });

    public static Error CapacityExceeded(int serviceId, int currentCount, int capacity) =>
        Error.Conflict(
            "FrequentSubscription.CapacityExceeded",
            $"No hay cupo: el service {serviceId} ya tiene {currentCount}/{capacity} suscripciones activas.");

    public static Error OverlapWithExistingSubscription(int customerId, int outboundServiceId) =>
        Error.Conflict(
            "FrequentSubscription.OverlapWithExistingSubscription",
            $"El cliente {customerId} ya tiene una suscripción activa para el service {outboundServiceId}.");
}

public enum SubscriptionLeg { Outbound, Inbound }
public enum DirectionKind { Pickup, Dropoff }
