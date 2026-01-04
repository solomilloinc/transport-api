using Transport.SharedKernel.Contracts.Service;

namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveReportResponseDto(int ReserveId, 
    string OriginName, 
    string DestinationName,
    int AvailableQuantity,
    int ReservedQuantity,
    string DepartureHour,
    int VehicleId, 
    int DriverId,
    DateTime ReserveDate,
    List<PassengerReserveReportResponseDto> Passengers,
    List<ReservePriceReport> Prices);
