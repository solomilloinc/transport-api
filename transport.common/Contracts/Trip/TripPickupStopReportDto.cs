namespace Transport.SharedKernel.Contracts.Trip;

public record TripPickupStopReportDto(
    int TripPickupStopId,
    int DirectionId,
    string DirectionName,
    int CityId,
    string CityName,
    int Order,
    TimeSpan PickupTimeOffset);
