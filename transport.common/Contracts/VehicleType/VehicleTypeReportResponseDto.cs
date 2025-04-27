namespace Transport.SharedKernel.Contracts.VehicleType;

public record VehicleTypeReportResponseDto(
    int VehicleTypeId,
    string Name,
    string? ImageBase64,
    int Quantity
);
