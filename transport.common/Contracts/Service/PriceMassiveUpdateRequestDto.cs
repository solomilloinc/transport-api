using System.Security.Cryptography.X509Certificates;

namespace Transport.SharedKernel.Contracts.Service;

public record PriceMassiveUpdateRequestDto(List<PricePercentageUpdateDto> PriceUpdates);

public record PricePercentageUpdateDto(int ReserveTypeId, decimal Percentage);

public record ServicePriceUpdateDto(
    int ReservePriceId, int ReserveTypeId, decimal Price);

public record ServicePriceAddDto(int ReserveTypeId, decimal Price);
