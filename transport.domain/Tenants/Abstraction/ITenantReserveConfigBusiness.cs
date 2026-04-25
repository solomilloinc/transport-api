using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Tenant;

namespace Transport.Domain.Tenants.Abstraction;

public interface ITenantReserveConfigBusiness
{
    Task<Result<TenantReserveConfigResponseDto>> Get(int tenantId);
    Task<Result<TenantReserveConfigResponseDto>> Update(int tenantId, TenantReserveConfigUpdateRequestDto request);
}
