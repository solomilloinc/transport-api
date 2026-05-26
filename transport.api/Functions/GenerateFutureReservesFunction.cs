using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.FrequentSubscriptions.Abstraction;
using Transport.Domain.Services.Abstraction;
using Transport.Infraestructure.Authentication;
using Transport.Infraestructure.Authorization;
using Transport.SharedKernel;

namespace transport_api.Functions;

public class GenerateFutureReservesFunction
{
    private readonly ILogger<GenerateFutureReservesFunction> _logger;
    private readonly IServiceBusiness _serviceBusiness;
    private readonly IFrequentPassengerBusiness _frequentPassengerBusiness;
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GenerateFutureReservesFunction(
        ILogger<GenerateFutureReservesFunction> logger,
        IServiceBusiness serviceBusiness,
        IFrequentPassengerBusiness frequentPassengerBusiness,
        IApplicationDbContext dbContext,
        ITenantContext tenantContext)
    {
        _logger = logger;
        _serviceBusiness = serviceBusiness;
        _frequentPassengerBusiness = frequentPassengerBusiness;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    [Function("GenerateFutureReservesFunction")]
    [Authorize("Admin", "SuperAdmin")]
    [OpenApiOperation(operationId: "GenerateFutureReserves", tags: new[] { "Reserves" })]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Future reserves generated successfully")]
    public async Task<HttpResponseData> Run(
     [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
     CancellationToken cancellationToken)
    {
        _logger.LogInformation("GenerateFutureReserves HTTP triggered at: {time}", DateTime.UtcNow);

        var tenantCount = await ExecuteAsync(cancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync($"Reserves generated for {tenantCount} tenants");
        return response;
    }

    [Function("GenerateFutureReservesTimerFunction")]
    public async Task RunTimer(
        [TimerTrigger("%GenerateFutureReservesCron%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "GenerateFutureReserves timer triggered at: {time}. Next scheduled at: {next}",
            DateTime.UtcNow,
            timer.ScheduleStatus?.Next);

        await ExecuteAsync(cancellationToken);
    }

    private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var tenants = await _dbContext.Tenants
            .Where(t => t.Status == EntityStatusEnum.Active)
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            if (_tenantContext is TenantContext tc)
            {
                tc.TenantId = tenant.TenantId;
                tc.TenantCode = tenant.Code;
            }

            var result = await _serviceBusiness.GenerateFutureReservesAsync();

            if (result.IsSuccess)
            {
                _logger.LogInformation("Future reserves generated for tenant {TenantCode}", tenant.Code);
            }
            else
            {
                _logger.LogError("Failed to generate reserves for tenant {TenantCode}: {Error}", tenant.Code, result.Error);
                continue;
            }

            var passengerResult = await _frequentPassengerBusiness.GenerateFrequentPassengersAsync();
            if (passengerResult.IsSuccess)
            {
                _logger.LogInformation("Frequent passengers generated for tenant {TenantCode}", tenant.Code);
            }
            else
            {
                _logger.LogError("Failed to generate frequent passengers for tenant {TenantCode}: {Error}",
                    tenant.Code, passengerResult.Error);
            }
        }

        return tenants.Count;
    }
}
