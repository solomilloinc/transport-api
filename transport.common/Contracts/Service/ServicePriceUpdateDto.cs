namespace Transport.SharedKernel.Contracts.Service;

public record ServicePriceUpdateDto(
    int ReservePriceId, decimal Price);
