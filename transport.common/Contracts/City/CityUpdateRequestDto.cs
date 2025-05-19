namespace Transport.SharedKernel.Contracts.City;

public record CityUpdateRequestDto(string Code, string Name, List<DirectionCreateRequestDto>? Directions);
