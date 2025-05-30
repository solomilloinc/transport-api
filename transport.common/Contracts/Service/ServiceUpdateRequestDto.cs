namespace Transport.SharedKernel.Contracts.Service;

public record ServiceUpdateRequestDto(
    string Name,
    int OriginId,
    int DestinationId,
    TimeSpan EstimatedDuration,
    int VehicleId,
    List<ServiceReservePriceDto> Prices
);

public record ServiceReservePriceDto(
    int ReserveTypeId,
    decimal Price
);
