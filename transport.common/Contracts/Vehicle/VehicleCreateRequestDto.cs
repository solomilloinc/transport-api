namespace Transport.SharedKernel.Contracts.Vehicle;

public record VehicleCreateRequestDto(
    int AvailableQuantity,
    string InternalNumber,
    int? VehicleTypeId
);
