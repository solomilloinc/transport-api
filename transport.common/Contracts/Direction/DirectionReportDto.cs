namespace Transport.SharedKernel.Contracts.Direction;

public record DirectionReportDto(
    int DirectionId,
    string Name,
    double? Lat,
    double? Lng,
    int CityId,
    string CityName,
    DateTime CreatedDate,
    DateTime? UpdatedDate
);
