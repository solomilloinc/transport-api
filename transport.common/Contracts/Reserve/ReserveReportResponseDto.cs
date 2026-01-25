using Transport.SharedKernel.Contracts.Trip;

namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveReportResponseDto(
    int ReserveId,
    int TripId,
    string TripName,
    string OriginName,
    string DestinationName,
    int AvailableQuantity,
    int ReservedQuantity,
    string DepartureHour,
    int VehicleId,
    int DriverId,
    DateTime ReserveDate);
