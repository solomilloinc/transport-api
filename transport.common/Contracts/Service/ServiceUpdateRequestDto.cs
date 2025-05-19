namespace Transport.SharedKernel.Contracts.Service;

public record ServiceUpdateRequestDto(
    string Name,
    int OriginId,
    int DestinationId,
    int StartDay,
    int EndDay,
    TimeSpan EstimatedDuration,
    TimeSpan DepartureHour,
    bool IsHoliday,
    int VehicleId,
    List<ServiceReservePriceDto> Prices
);

public record ServiceReservePriceDto(
    int ReserveTypeId,
    decimal Price
);
