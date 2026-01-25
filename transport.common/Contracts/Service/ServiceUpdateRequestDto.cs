namespace Transport.SharedKernel.Contracts.Service;

public record ServiceUpdateRequestDto(
    string Name,
    TimeSpan EstimatedDuration,
    int VehicleId,
    List<int>? AllowedDirectionIds = null
);
