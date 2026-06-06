
namespace Transport.SharedKernel.Contracts.City;

public record CityReportResponseDto(int Id, string Name, string Code, string Status, List<DirectionsReportDto> Directions);

public record DirectionsReportDto(
    int Id,
    string Name,
    double? Lat,
    double? Lng);
