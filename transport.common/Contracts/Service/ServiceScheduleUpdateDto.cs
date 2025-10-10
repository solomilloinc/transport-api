namespace Transport.SharedKernel.Contracts.Service;

public record ServiceScheduleUpdateDto(
    TimeSpan DepartureHour,
    bool IsHoliday);
