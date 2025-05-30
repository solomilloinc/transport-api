namespace Transport.SharedKernel.Contracts.Service;

public record ServiceScheduleCreateDto(int ServiceId,
    int DayOfWeek,
    DayOfWeek StartDay,
    DayOfWeek EndDay,
    bool IsHoliday,
    TimeSpan DepartureHour);
