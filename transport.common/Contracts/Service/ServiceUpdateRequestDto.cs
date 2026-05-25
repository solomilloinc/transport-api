namespace Transport.SharedKernel.Contracts.Service;

public record ServiceUpdateRequestDto(
    string Name,
    int TripId,
    int VehicleId,
    DayOfWeek DayOfWeek,
    TimeSpan DepartureHour,
    TimeSpan EstimatedDuration,
    bool IsHoliday = false,
    List<int>? AllowedDirectionIds = null);
