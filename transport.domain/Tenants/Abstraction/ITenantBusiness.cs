using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Tenant;

namespace Transport.Domain.Tenants.Abstraction;

public interface ITenantBusiness
{
    Task<Result<TenantResponseDto>> Create(TenantCreateRequestDto request);
    Task<Result<TenantResponseDto>> Update(int tenantId, TenantUpdateRequestDto request);
    Task<Result<bool>> Delete(int tenantId);
    Task<Result<List<TenantResponseDto>>> GetAll();
    Task<Result<bool>> UpdatePaymentConfig(int tenantId, TenantPaymentConfigUpdateRequestDto request);
    Task<Result<bool>> UpdateTenantConfig(int tenantId, TenantConfigUpdateRequestDto request);
    Task<Result<string>> GetTenantConfig();
    Task<Result<string>> ResolveTenantByHost(string host);
}
