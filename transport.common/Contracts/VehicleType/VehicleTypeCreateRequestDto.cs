using Transport.SharedKernel.Contracts.Vehicle;

namespace Transport.SharedKernel.Contracts.VehicleType;

public record VehicleTypeCreateRequestDto(
    string Name,
    string? ImageBase64,
    int Quantity,
    List<VehicleCreateRequestDto> Vehicles
);
