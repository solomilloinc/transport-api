namespace Transport.SharedKernel.Contracts.Service;

public record ServiceScheduleCreateDto(int ServiceId,
    bool IsHoliday,
    TimeSpan DepartureHour);
