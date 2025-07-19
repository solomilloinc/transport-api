namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveExternalReportResponseDto(int ReserveId,
    string OriginName,
    string DestinationName,
    string DepartureHour,
    decimal Price);
