namespace Transport.SharedKernel.Contracts.Service;

public record ServiceReportResponseDto(
    int ServiceId,
    string Name,
    string OriginName,
    string DestinationName,
    TimeSpan EstimatedDuration,
    TimeSpan DepartureHour,
    bool IsHoliday,
    ServiceVehicleResponseDto Vehicle,
    string Status,
    List<ReservePriceReport> ReservePrices);

public record ServiceVehicleResponseDto(string InternalNumber, int AvailableQuantity, int FullQuantity, string VehicleTypeName, string? Image);

public record ReservePriceReport(int ReserveTypeId, decimal Price);