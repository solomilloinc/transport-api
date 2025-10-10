namespace Transport.SharedKernel.Contracts.Service;

public record ServiceScheduleReportResponseDto(
    int ServiceScheduleId,
    int ServiceId,
    TimeSpan DepartureHour,
    bool IsHoliday,
    string Status);