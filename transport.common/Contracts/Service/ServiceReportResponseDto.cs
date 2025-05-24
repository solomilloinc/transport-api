namespace Transport.SharedKernel.Contracts.Service;

public record ServiceReportResponseDto(
    int ServiceId,
    string Name,
    int OriginId,
    string OriginName,
    int DestinationId,
    string DestinationName,
    TimeSpan EstimatedDuration,
    TimeSpan DepartureHour,
    DayOfWeek StartDay,
    DayOfWeek EndDay,
    bool IsHoliday,
    ServiceVehicleResponseDto Vehicle,
    string Status,
    List<ReservePriceReport> ReservePrices);

public record ServiceVehicleResponseDto(int VehicleId, string InternalNumber, int AvailableQuantity, int FullQuantity, string VehicleTypeName, string? Image);

public record ReservePriceReport(int ReserveTypeId, decimal Price);