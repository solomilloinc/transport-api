namespace Transport.SharedKernel.Contracts.City;

public record DirectionCreateRequestDto(
    string Name,
    double? Lat,
    double? Lng);
