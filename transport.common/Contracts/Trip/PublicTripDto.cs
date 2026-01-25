namespace Transport.SharedKernel.Contracts.Trip;

/// <summary>
/// DTO for public trip listing (landing page)
/// </summary>
public record PublicTripDto(
    int TripId,
    string Description,
    int OriginCityId,
    string OriginCityName,
    int DestinationCityId,
    string DestinationCityName,
    decimal? PriceFrom,
    string? EstimatedDuration
);
