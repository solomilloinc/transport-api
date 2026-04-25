using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using System.Net;
using Transport_Api.Functions.Base;
using Transport.Domain.Tenants.Abstraction;
using Transport.Infraestructure.Authorization;
using Transport.SharedKernel.Contracts.Tenant;

namespace Transport_Api.Functions;

public sealed class TenantReserveConfigFunction : FunctionBase
{
    private readonly ITenantReserveConfigBusiness _business;

    public TenantReserveConfigFunction(ITenantReserveConfigBusiness business, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _business = business;
    }

    [Function("GetTenantReserveConfig")]
    [Authorize("SuperAdmin")]
    [OpenApiOperation(operationId: "tenant-reserve-config-get", tags: new[] { "Tenant" }, Summary = "Get Tenant Reserve Config", Description = "Returns reserve business rules for a tenant (SuperAdmin only).", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tenantId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Tenant ID")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(TenantReserveConfigResponseDto), Summary = "Reserve Config")]
    public async Task<HttpResponseData> GetTenantReserveConfig(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:int}/reserve-config")] HttpRequestData req,
        int tenantId)
    {
        var result = await _business.Get(tenantId);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateTenantReserveConfig")]
    [Authorize("SuperAdmin")]
    [OpenApiOperation(operationId: "tenant-reserve-config-update", tags: new[] { "Tenant" }, Summary = "Update Tenant Reserve Config", Description = "Updates reserve business rules for a tenant (SuperAdmin only).", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tenantId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Tenant ID")]
    [OpenApiRequestBody("application/json", typeof(TenantReserveConfigUpdateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(TenantReserveConfigResponseDto), Summary = "Reserve Config Updated")]
    public async Task<HttpResponseData> UpdateTenantReserveConfig(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tenants/{tenantId:int}/reserve-config")] HttpRequestData req,
        int tenantId)
    {
        var dto = await req.ReadFromJsonAsync<TenantReserveConfigUpdateRequestDto>();
        var result = await _business.Update(tenantId, dto!);
        return await MatchResultAsync(req, result);
    }
}
