namespace Transport.SharedKernel.Contracts.VehicleType;

public record VehicleTypeUpdateRequestDto(string Name, string? ImageBase64, int Quantity);
