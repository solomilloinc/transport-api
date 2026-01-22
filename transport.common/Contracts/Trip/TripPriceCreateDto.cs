namespace Transport.SharedKernel.Contracts.Trip;

public record TripPriceCreateDto(
    int TripId,
    int CityId,
    int? DirectionId,
    int ReserveTypeId,
    decimal Price,
    int Order);
