namespace Transport.SharedKernel.Contracts.Trip;

public record TripCreateDto(
    string Description,
    int OriginCityId,
    int DestinationCityId);
