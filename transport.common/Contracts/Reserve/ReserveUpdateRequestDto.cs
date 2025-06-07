namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveUpdateRequestDto(
int? VehicleId,
int? DriverId,
DateTime? ReserveDate,
TimeSpan? DepartureHour,
int? Status
);
