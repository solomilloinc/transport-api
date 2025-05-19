namespace Transport.SharedKernel.Contracts.Vehicle;

public record VehicleResponseDto(
    int VehicleId,
    int VehicleTypeId,
    string InternalNumber,
    string VehicleTypeName
);