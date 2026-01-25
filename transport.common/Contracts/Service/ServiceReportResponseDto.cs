namespace Transport.SharedKernel.Contracts.Service;

public record ServiceReportResponseDto(
    int ServiceId,
    string Name,
    int TripId,
    int OriginId,
    string OriginName,
    int DestinationId,
    string DestinationName,
    TimeSpan EstimatedDuration,
    DayOfWeek StartDay,
    DayOfWeek EndDay,
    ServiceVehicleResponseDto Vehicle,
    string Status,
    List<ServiceScheduleReportResponseDto> Schedulers,
    List<ServiceDirectionResponseDto> AllowedDirections);

public record ServiceDirectionResponseDto(
    int DirectionId,
    string Name,
    int CityId);
