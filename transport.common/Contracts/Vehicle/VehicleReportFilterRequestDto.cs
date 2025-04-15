namespace Transport.SharedKernel.Contracts.Vehicle;

public record VehicleReportFilterRequestDto(
    int? VehicleTypeId,
    string? InternalNumber
);
