namespace Transport.SharedKernel.Contracts.Service;

public record ServiceScheduleCreateDto(int ServiceId,
    DayOfWeek StartDay,
    DayOfWeek EndDay,
    bool IsHoliday,
    TimeSpan DepartureHour);
