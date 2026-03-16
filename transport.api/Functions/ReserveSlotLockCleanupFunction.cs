using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.Reserves.Abstraction;
using Transport.Infraestructure.Authentication;
using Transport.SharedKernel;
using Transport.SharedKernel.Configuration;

namespace transport_api.Functions;

public class ReserveSlotLockCleanupFunction
{
    private readonly ILogger<ReserveSlotLockCleanupFunction> _logger;
    private readonly IReserveBusiness _reserveBusiness;
    private readonly IReserveOption _reserveOptions;
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ReserveSlotLockCleanupFunction(
        ILogger<ReserveSlotLockCleanupFunction> logger,
        IReserveBusiness reserveBusiness,
        IReserveOption reserveOptions,
        IApplicationDbContext dbContext,
        ITenantContext tenantContext)
    {
        _logger = logger;
        _reserveBusiness = reserveBusiness;
        _reserveOptions = reserveOptions;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    [Function("ReserveSlotLockCleanup")]
    public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation(
            "Starting ReserveSlotLock cleanup process at: {time}",
            DateTime.UtcNow
        );

        try
        {
            var tenants = await _dbContext.Tenants
                .Where(t => t.Status == EntityStatusEnum.Active)
                .ToListAsync();

            foreach (var tenant in tenants)
            {
                if (_tenantContext is TenantContext tc)
                {
                    tc.TenantId = tenant.TenantId;
                    tc.TenantCode = tenant.Code;
                }

                var result = await _reserveBusiness.CleanupExpiredReserveSlotLocks();

                if (result.IsSuccess)
                {
                    _logger.LogInformation("SlotLock cleanup completed for tenant {TenantCode}", tenant.Code);
                }
                else
                {
                    _logger.LogError("SlotLock cleanup failed for tenant {TenantCode}: {Error}", tenant.Code, result.Error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during ReserveSlotLock cleanup at: {time}", DateTime.UtcNow);
        }

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextRun}", myTimer.ScheduleStatus.Next);
        }
    }

    [Function("ReserveSlotLockCleanupManual")]
    public async Task<HttpResponseData> RunManual([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Manual ReserveSlotLock cleanup triggered at: {time}", DateTime.UtcNow);

        try
        {
            var tenants = await _dbContext.Tenants
                .Where(t => t.Status == EntityStatusEnum.Active)
                .ToListAsync();

            foreach (var tenant in tenants)
            {
                if (_tenantContext is TenantContext tc)
                {
                    tc.TenantId = tenant.TenantId;
                    tc.TenantCode = tenant.Code;
                }

                await _reserveBusiness.CleanupExpiredReserveSlotLocks();
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteStringAsync($"Cleanup completed for {tenants.Count} tenants");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during manual cleanup");
            var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Exception: {ex.Message}");
            return response;
        }
    }
}
