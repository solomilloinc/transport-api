using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.Tenants;
using Transport.Infraestructure.Authentication;
using Transport.SharedKernel;

namespace Transport_Api.Middleware;

public class TenantResolutionMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private static readonly HashSet<string> ExcludedPaths = new()
    {
        "RenderSwaggerUI",
        "RenderSwaggerDocument",
        "OutboxTimerFunction",
        "ReserveSlotLockCleanup",
        "ReserveSlotLockCleanupManual",
        "RefreshTokenCleanup",
        "RefreshTokenCleanupManual",
        "MPWebhook",
        "WalletForSuccess",
        "CustomerReserveCreatedSubscriptionFunction",
        "ResolveTenant",
    };

    public TenantResolutionMiddleware(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var functionName = context.FunctionDefinition.Name;

        if (ExcludedPaths.Contains(functionName))
        {
            await next(context);
            return;
        }

        var request = await context.GetHttpRequestDataAsync();

        // Non-HTTP triggers (timers, service bus) skip tenant resolution
        if (request is null)
        {
            await next(context);
            return;
        }

        request.Headers.TryGetValues("X-Tenant-Code", out var tenantCodeValues);

        var tenantCode = tenantCodeValues?.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            throw new TenantResolutionException("X-Tenant-Code or X-Tenant-Domain header is required");
        }

        var dbContext = context.InstanceServices.GetService(typeof(IApplicationDbContext)) as IApplicationDbContext;

        Tenant? tenant = null;

        if (!string.IsNullOrWhiteSpace(tenantCode))
        {
            tenant = await ResolveTenantByCode(dbContext!, tenantCode);
        }

        if (tenant is null || tenant.Status != EntityStatusEnum.Active)
        {
            throw new TenantResolutionException("Tenant not found or inactive");
        }

        var tenantContext = context.InstanceServices.GetService(typeof(ITenantContext)) as TenantContext;
        tenantContext!.TenantId = tenant.TenantId;
        tenantContext.TenantCode = tenant.Code;

        await next(context);
    }

    private async Task<Tenant?> ResolveTenantByCode(IApplicationDbContext dbContext, string code)
    {
        var cacheKey = $"tenant:code:{code.ToLowerInvariant()}";

        if (_cache.TryGetValue(cacheKey, out Tenant? cached))
            return cached;

        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Code == code);

        if (tenant is not null)
        {
            _cache.Set(cacheKey, tenant, CacheDuration);
        }

        return tenant;
    }

    private async Task<Tenant?> ResolveTenantByDomain(IApplicationDbContext dbContext, string domain)
    {
        var cacheKey = $"tenant:domain:{domain.ToLowerInvariant()}";

        if (_cache.TryGetValue(cacheKey, out Tenant? cached))
            return cached;

        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Domain == domain);

        if (tenant is not null)
        {
            _cache.Set(cacheKey, tenant, CacheDuration);
        }

        return tenant;
    }
}

/// <summary>
/// Custom exception for tenant resolution failures.
/// Caught by ExceptionHandlingMiddleware to return 400 Bad Request.
/// </summary>
public class TenantResolutionException : Exception
{
    public TenantResolutionException(string message) : base(message) { }
}
