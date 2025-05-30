namespace Transport.SharedKernel.Contracts.Service;

public record ServiceReportResponseDto(
    int ServiceId,
    string Name,
    int OriginId,
    string OriginName,
    int DestinationId,
    string DestinationName,
    TimeSpan EstimatedDuration,
    ServiceVehicleResponseDto Vehicle,
    string Status,
    List<ReservePriceReport> ReservePrices,
    List<ServiceScheduleReportResponseDto> Schedulers);
