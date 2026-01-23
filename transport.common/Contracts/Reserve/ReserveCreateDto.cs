namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveCreateDto(
    DateTime ReserveDate,
    int VehicleId,
    int? DriverId,
    int TripId,
    TimeSpan DepartureHour,
    TimeSpan EstimatedDuration,
    bool IsHoliday = false,
    List<int>? AllowedDirectionIds = null
);
