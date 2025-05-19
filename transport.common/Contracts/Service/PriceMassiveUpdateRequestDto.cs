namespace Transport.SharedKernel.Contracts.Service;

public record PriceMassiveUpdateRequestDto(List<PricePercentageUpdateDto> PriceUpdates);
