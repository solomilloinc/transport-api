
namespace Transport.SharedKernel.Contracts.City;

public record CityReportResponseDto(int Id, string Name, string Code, List<DirectionsReportDto> Directions);

public record DirectionsReportDto(
    int Id,
    string Name,
    double? Lat,
    double? Lng);
