namespace Transport.SharedKernel.Contracts.Service;

public record ServiceCreateRequestDto(string Name,
    int OriginId,
    int DestinationId,
    TimeSpan EstimatedDuration,
    TimeSpan DepartureHour,
    bool IsHoliday,
    int VehicleId,
    int StartDay,
    int EndDay,
    List<ReservePriceCreateRequestDto> Prices);

public record ReservePriceCreateRequestDto(
    decimal Price,
    int ReserveTypeId);
