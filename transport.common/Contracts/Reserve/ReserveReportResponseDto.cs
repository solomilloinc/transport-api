namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveReportResponseDto(int ReserveId, 
    string OriginName, 
    string DestinationName,
    int AvailableQuantity,
    int ReservedQuantity,
    List<CustomerReserveReportResponseDto> Passengers);
