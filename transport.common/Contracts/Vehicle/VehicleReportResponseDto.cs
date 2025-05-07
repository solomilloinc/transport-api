namespace Transport.SharedKernel.Contracts.Vehicle;

public record VehicleReportResponseDto(int VehicleId,
    int VehicleTypeId,
    string InternalNumber,
    string VehicleTypeName,
    int VehicleTypeQuantity,
    string? ImageBase64,
    string Status,
    int AvailableQuantity
);
