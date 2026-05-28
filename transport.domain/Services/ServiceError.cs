using Transport.SharedKernel;

namespace Transport.Domain.Services;

public static class ServiceError
{
    public static readonly Error ServiceNotFound = new(
            "Service.NotFound",
            "The service you are looking for does not exist",
            ErrorType.NotFound
        );

    public static Error SlotConflict(int tripId, DayOfWeek dayOfWeek, TimeSpan departureHour) =>
        Error.Conflict(
            "Service.SlotConflict",
            $"Ya existe un servicio activo para el tramo {tripId} los días {dayOfWeek} a las {departureHour:hh\\:mm}."
        );

    public static Error ServiceNotActive(int serviceId) =>
          new Error(
              "Service.ServiceNotActive",
              $"El servicio con Id {serviceId} no existe o no está activo",
              ErrorType.Validation
          );

    public static Error HasActiveSubscriptions(int serviceId, int count) =>
        Error.Conflict(
            "Service.HasActiveSubscriptions",
            $"No se puede modificar el service {serviceId}: tiene {count} suscripciones frecuentes activas. Cancelálas primero."
        );

    public static Error VehicleCapacityBelowSubscriptions(int serviceId, int newCapacity, int subsCount) =>
        Error.Conflict(
            "Service.VehicleCapacityBelowSubscriptions",
            $"El vehículo elegido tiene capacidad {newCapacity} pero el service {serviceId} ya tiene {subsCount} suscripciones activas. Cancelá suscripciones o elegí un vehículo más grande."
        );
}
