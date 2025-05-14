using System.Security.Cryptography.X509Certificates;

namespace Transport.SharedKernel.Contracts.Service;

public record PriceMassiveUpdateRequestDto(List<PriceUpdateDto> PriceUpdates);

public record PriceUpdateDto(int ReserveTypeId, decimal Percentage);
