namespace Transport.SharedKernel.Contracts.Service;

public record ServiceVehicleResponseDto(int VehicleId, string InternalNumber, int AvailableQuantity, int FullQuantity, string VehicleTypeName, string? Image);
