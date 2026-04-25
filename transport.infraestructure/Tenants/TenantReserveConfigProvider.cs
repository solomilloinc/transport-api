using Microsoft.EntityFrameworkCore;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.Tenants;
using Transport.Domain.Tenants.Abstraction;

namespace Transport.Infraestructure.Tenants;

/// <summary>
/// Scoped implementation that caches the reserve configuration for the
/// current tenant for the lifetime of the request, avoiding repeated DB hits
/// during a single business operation (e.g. multiple price lookups in a quote).
/// </summary>
public sealed class TenantReserveConfigProvider : ITenantReserveConfigProvider
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private TenantReserveConfig? _cached;

    public TenantReserveConfigProvider(IApplicationDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<TenantReserveConfig> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
            return _cached;

        var tenantId = _tenantContext.TenantId;

        var config = await _context.TenantReserveConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

        _cached = config ?? new TenantReserveConfig
        {
            TenantId = tenantId,
            RoundTripSameDayOnly = true
        };

        return _cached;
    }
}
