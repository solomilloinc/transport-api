namespace Transport.SharedKernel.Contracts.City;

public record CityReportFilterRequestDto(string Name, string Code, EntityStatusEnum? Status);
