namespace Transport.SharedKernel.Contracts.Direction;

public record DirectionCreateDto(
  string Name,
  double? Lat,
  double? Lng,
  int CityId
);
