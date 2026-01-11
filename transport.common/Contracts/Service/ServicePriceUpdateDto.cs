namespace Transport.SharedKernel.Contracts.Service;

public record ServicePriceUpdateDto(
    int ReserveTypeId, decimal Price);
