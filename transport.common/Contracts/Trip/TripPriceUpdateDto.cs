namespace Transport.SharedKernel.Contracts.Trip;

public record TripPriceUpdateDto(
    int CityId,
    int? DirectionId,
    int ReserveTypeId,
    decimal Price,
    int Order);
