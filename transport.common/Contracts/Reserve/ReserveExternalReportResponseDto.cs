namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveExternalReportResponseDto(int ReserveId,
    string OriginName,
    string DestinationName,
    string DepartureHour,
    DateTime DepartureDate,
    string EstimatedDuration,
    string ArrivalHour,
    decimal Price,
    int AvailableQuantity,
    string VehicleName);
