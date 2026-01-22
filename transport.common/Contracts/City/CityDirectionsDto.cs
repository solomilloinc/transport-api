using Transport.SharedKernel.Contracts.Direction;

namespace Transport.SharedKernel.Contracts.City;

public record CityDirectionsDto(
    int CityId,
    string Name,
    List<DirectionDto> Directions
);
