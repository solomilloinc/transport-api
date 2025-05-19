namespace Transport.SharedKernel.Contracts.VehicleType;

public record VehicleTypeReportFilterRequestDto(
    int? VehicleTypeId,
    string? Name
);
