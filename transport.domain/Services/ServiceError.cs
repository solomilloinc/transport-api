using Transport.SharedKernel;

namespace Transport.Domain.Services;

public static class ServiceError
{
    public static readonly Error ServiceNotFound = new(
            "ServiceNotFound",
            "The service you are looking for does not exist",
            ErrorType.NotFound
        );

    public static readonly Error ServiceScheduleNotFound = new(
            "ServiceScheduleNotFound",
            "The service schedule you are looking for does not exist",
            ErrorType.NotFound
        );

    /// <summary>
    /// Usado por <c>ServiceBusiness.SyncSchedules</c> cuando el payload incluye un
    /// <c>ServiceScheduleId</c> que no pertenece al servicio indicado en la ruta.
    /// Señal de bug en el frontend o intento de manipulación; se rechaza la operación
    /// completa (no se hace fallback a "crear") para no corromper schedules de otro servicio.
    /// </summary>
    public static Error ScheduleNotInService(int scheduleId, int serviceId) =>
          new Error(
              "Service.ScheduleNotInService",
              $"El schedule {scheduleId} no pertenece al servicio {serviceId}.",
              ErrorType.Validation
          );

    public static readonly Error InvalidDayRange = new(
            "Service.InvalidDayRange",
            "La fecha desde no puede ser mayor a la fecha hasta",
            ErrorType.Validation
        );

    public static Error ScheduleConflict(DayOfWeek startDay, DayOfWeek endDay, TimeSpan departureHour) =>
          new Error(
              "Service.ScheduleConflict",
              $"Hay conflictos de días y horarios en el servicio para el día {startDay} y {endDay} a las {departureHour}.",
              ErrorType.Validation
          );

    public static Error ServiceNotActive(int serviceId) =>
          new Error(
              "Service.ServiceNotActive",
              $"El servicio con Id {serviceId} no existe o no está activo",
              ErrorType.Validation
          );
}
