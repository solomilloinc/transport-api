namespace Transport.SharedKernel.Contracts.Reserve;

public record CustomerPendingReserveDto(
    int ReserveId,
    DateTime ReserveDate,
    string OriginName,
    string DestinationName,
    string DepartureHour,
    decimal TotalPrice,
    decimal TotalPaid,
    decimal PendingDebt,
    List<CustomerPendingPassengerDto> Passengers);

public record CustomerPendingPassengerDto(
    int PassengerId,
    string FullName,
    decimal Price,
    int Status);
