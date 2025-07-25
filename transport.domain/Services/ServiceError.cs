﻿using Transport.SharedKernel;

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
}
