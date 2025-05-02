namespace Transport.SharedKernel.Contracts.Service;

public record ServiceReportFilterRequestDto(string? Name, 
    int? OriginId, 
    int? DestinationId, 
    bool? IsHoliday, 
    int? VehicleId, 
    EntityStatusEnum? Status);
