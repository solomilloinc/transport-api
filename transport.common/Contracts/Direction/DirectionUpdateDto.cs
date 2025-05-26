namespace Transport.SharedKernel.Contracts.Direction;

public record DirectionUpdateDto(
    string Name,
    double? Lat,
    double? Lng,
    int CityId
);
