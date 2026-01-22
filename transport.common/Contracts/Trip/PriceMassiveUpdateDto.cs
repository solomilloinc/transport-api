namespace Transport.SharedKernel.Contracts.Trip;

public record PriceMassiveUpdateDto(List<PriceUpdateItem> PriceUpdates);

public record PriceUpdateItem(int ReserveTypeId, decimal Percentage);
