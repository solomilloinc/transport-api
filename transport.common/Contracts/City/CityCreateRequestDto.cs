namespace Transport.SharedKernel.Contracts.City;

public record CityCreateRequestDto(string Code, string Name, List<DirectionCreateRequestDto> Directions);
