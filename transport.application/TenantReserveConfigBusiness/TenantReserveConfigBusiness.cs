using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Tenants;
using Transport.Domain.Tenants.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Tenant;

namespace Transport.Business.TenantReserveConfigBusiness;

public class TenantReserveConfigBusiness : ITenantReserveConfigBusiness
{
    private readonly IApplicationDbContext _context;

    public TenantReserveConfigBusiness(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TenantReserveConfigResponseDto>> Get(int tenantId)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId);

        if (tenant is null)
            return Result.Failure<TenantReserveConfigResponseDto>(TenantError.NotFound);

        var config = await _context.TenantReserveConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId);

        // Default to safe behavior when no explicit config exists.
        var roundTripSameDayOnly = config?.RoundTripSameDayOnly ?? true;

        return new TenantReserveConfigResponseDto(tenantId, roundTripSameDayOnly);
    }

    public async Task<Result<TenantReserveConfigResponseDto>> Update(int tenantId, TenantReserveConfigUpdateRequestDto request)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId);

        if (tenant is null)
            return Result.Failure<TenantReserveConfigResponseDto>(TenantError.NotFound);

        var existing = await _context.TenantReserveConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId);

        if (existing is not null)
        {
            existing.RoundTripSameDayOnly = request.RoundTripSameDayOnly;
            _context.TenantReserveConfigs.Update(existing);
        }
        else
        {
            var config = new TenantReserveConfig
            {
                TenantId = tenantId,
                RoundTripSameDayOnly = request.RoundTripSameDayOnly
            };
            _context.TenantReserveConfigs.Add(config);
        }

        await _context.SaveChangesWithOutboxAsync();

        return new TenantReserveConfigResponseDto(tenantId, request.RoundTripSameDayOnly);
    }
}
