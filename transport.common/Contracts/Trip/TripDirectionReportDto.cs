namespace Transport.SharedKernel.Contracts.Trip;

public record TripDirectionReportDto(
    int TripDirectionId,
    int DirectionId,
    string DirectionName,
    int CityId,
    string CityName,
    int Order,
    TimeSpan PickupTimeOffset);
