namespace Transport.SharedKernel.Contracts.Service;

public record ServiceCreateRequestDto(
    string Name,
    int TripId,
    int OriginId,
    int DestinationId,
    TimeSpan EstimatedDuration,
    int VehicleId,
    List<ServiceScheduleCreateDto> Schedules,
    List<int>? AllowedDirectionIds = null);
