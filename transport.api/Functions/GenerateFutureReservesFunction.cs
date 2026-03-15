using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.Services.Abstraction;
using Transport.Infraestructure.Authentication;
using Transport.Infraestructure.Authorization;
using Transport.SharedKernel;

namespace transport_api.Functions;

public class GenerateFutureReservesFunction
{
    private readonly ILogger<GenerateFutureReservesFunction> _logger;
    private readonly IServiceBusiness _serviceBusiness;
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GenerateFutureReservesFunction(
        ILogger<GenerateFutureReservesFunction> logger,
        IServiceBusiness serviceBusiness,
        IApplicationDbContext dbContext,
        ITenantContext tenantContext)
    {
        _logger = logger;
        _serviceBusiness = serviceBusiness;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    [Function("GenerateFutureReservesFunction")]
    [Authorize("Admin", "SuperAdmin")]
    [OpenApiOperation(operationId: "GenerateFutureReserves", tags: new[] { "Reserves" })]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Future reserves generated successfully")]
    public async Task<HttpResponseData> Run(
     [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("GenerateFutureReserves triggered at: {time}", DateTime.UtcNow);

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

            var result = await _serviceBusiness.GenerateFutureReservesAsync();

            if (result.IsSuccess)
            {
                _logger.LogInformation("Future reserves generated for tenant {TenantCode}", tenant.Code);
            }
            else
            {
                _logger.LogError("Failed to generate reserves for tenant {TenantCode}: {Error}", tenant.Code, result.Error);
            }
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync($"Reserves generated for {tenants.Count} tenants");
        return response;
    }
}
