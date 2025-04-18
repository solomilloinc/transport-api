namespace Transport.SharedKernel.Contracts.Vehicle;

public record VehicleCreateRequestDto(
    int VehicleTypeId,
    string InternalNumber
);
