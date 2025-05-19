namespace Transport.SharedKernel.Contracts.Vehicle;

public record VehicleUpdateRequestDto(
    int VehicleTypeId,
    string InternalNumber,
    int AvailableQuantity
);