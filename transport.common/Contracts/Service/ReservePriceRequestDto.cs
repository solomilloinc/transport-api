namespace Transport.SharedKernel.Contracts.Service;

public record ReservePriceRequestDto(
    decimal Price,
    int ReserveTypeId);
