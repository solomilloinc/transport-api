namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveReportFilterRequestDto(
    int TripId,
    string TripType,
    int Passengers,
    DateTime DepartureDate,
    DateTime? ReturnDate,
    int? PickupDirectionId = null);
