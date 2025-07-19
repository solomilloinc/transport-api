namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveReportFilterRequestDto(int OriginId, 
    int DestinationId, 
    string TripType, 
    int Passengers,
    DateTime DepartureDate,
    DateTime? ReturnDate);
