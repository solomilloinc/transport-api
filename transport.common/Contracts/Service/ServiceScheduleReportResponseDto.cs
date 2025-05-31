namespace Transport.SharedKernel.Contracts.Service;

public record ServiceScheduleReportResponseDto(
    int ServiceScheduleId,
    int ServiceId,
    DayOfWeek StartDay,
    DayOfWeek EndDay,
    TimeSpan DepartureHour,
    bool IsHoliday,
    string Status);