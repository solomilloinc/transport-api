namespace Transport.SharedKernel.Contracts.Service;

public record ServiceUpdateRequestDto(
    string Name,
    int OriginId,
    int DestinationId,
    TimeSpan EstimatedDuration,
    int VehicleId
);
