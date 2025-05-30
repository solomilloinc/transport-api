namespace Transport.SharedKernel.Contracts.Service;

public record ServiceScheduleUpdateDto(
    int DayOfWeek,
    DayOfWeek StartDay,
    DayOfWeek EndDay,
    TimeSpan DepartureHour,
    bool IsHoliday);
