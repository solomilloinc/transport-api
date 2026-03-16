using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using System.Net;
using Transport_Api.Functions.Base;
using Transport.Infraestructure.Authorization;
using Transport.Domain.Tenants.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Tenant;

namespace Transport_Api.Functions;

public sealed class TenantFunction : FunctionBase
{
    private readonly ITenantBusiness _tenantBusiness;

    public TenantFunction(ITenantBusiness tenantBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _tenantBusiness = tenantBusiness;
    }

    [Function("CreateTenant")]
    [Authorize("SuperAdmin")]
    [OpenApiOperation(operationId: "tenant-create", tags: new[] { "Tenant" }, Summary = "Create Tenant", Description = "Creates a new tenant (SuperAdmin only)", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(TenantCreateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(TenantResponseDto), Summary = "Tenant Created")]
    public async Task<HttpResponseData> CreateTenant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<TenantCreateRequestDto>();
        var result = await _tenantBusiness.Create(dto!);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateTenant")]
    [Authorize("SuperAdmin")]
    [OpenApiOperation(operationId: "tenant-update", tags: new[] { "Tenant" }, Summary = "Update Tenant", Description = "Updates an existing tenant (SuperAdmin only)", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tenantId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Tenant ID")]
    [OpenApiRequestBody("application/json", typeof(TenantUpdateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(TenantResponseDto), Summary = "Tenant Updated")]
    public async Task<HttpResponseData> UpdateTenant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tenants/{tenantId:int}")] HttpRequestData req,
        int tenantId)
    {
        var dto = await req.ReadFromJsonAsync<TenantUpdateRequestDto>();
        var result = await _tenantBusiness.Update(tenantId, dto!);
        return await MatchResultAsync(req, result);
    }

    [Function("DeleteTenant")]
    [Authorize("SuperAdmin")]
    [OpenApiOperation(operationId: "tenant-delete", tags: new[] { "Tenant" }, Summary = "Delete Tenant", Description = "Deactivates a tenant (SuperAdmin only)", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tenantId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Tenant ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Tenant Deleted")]
    public async Task<HttpResponseData> DeleteTenant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tenants/{tenantId:int}")] HttpRequestData req,
        int tenantId)
    {
        var result = await _tenantBusiness.Delete(tenantId);
        return await MatchResultAsync(req, result);
    }

    [Function("GetAllTenants")]
    [Authorize("SuperAdmin")]
    [OpenApiOperation(operationId: "tenant-list", tags: new[] { "Tenant" }, Summary = "List Tenants", Description = "Returns all active tenants (SuperAdmin only)", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(List<TenantResponseDto>), Summary = "Tenant List")]
    public async Task<HttpResponseData> GetAllTenants(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants")] HttpRequestData req)
    {
        var result = await _tenantBusiness.GetAll();
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateTenantPaymentConfig")]
    [Authorize("SuperAdmin")]
    [OpenApiOperation(operationId: "tenant-payment-config", tags: new[] { "Tenant" }, Summary = "Update Payment Config", Description = "Updates MercadoPago credentials for a tenant (SuperAdmin only)", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tenantId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Tenant ID")]
    [OpenApiRequestBody("application/json", typeof(TenantPaymentConfigUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Payment Config Updated")]
    public async Task<HttpResponseData> UpdateTenantPaymentConfig(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tenants/{tenantId:int}/payment-config")] HttpRequestData req,
        int tenantId)
    {
        var dto = await req.ReadFromJsonAsync<TenantPaymentConfigUpdateRequestDto>();
        var result = await _tenantBusiness.UpdatePaymentConfig(tenantId, dto!);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateTenantConfig")]
    [Authorize("SuperAdmin")]
    [OpenApiOperation(operationId: "tenant-config-update", tags: new[] { "Tenant" }, Summary = "Update Tenant Config", Description = "Updates tenant configuration (identity, contact, legal, styles). SuperAdmin only.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tenantId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Tenant ID")]
    [OpenApiRequestBody("application/json", typeof(TenantConfigUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Tenant Config Updated")]
    public async Task<HttpResponseData> UpdateTenantConfig(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tenants/{tenantId:int}/config")] HttpRequestData req,
        int tenantId)
    {
        var dto = await req.ReadFromJsonAsync<TenantConfigUpdateRequestDto>();
        var result = await _tenantBusiness.UpdateTenantConfig(tenantId, dto!);
        return await MatchResultAsync(req, result);
    }

    [Function("GetTenantConfig")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "tenant-config", tags: new[] { "Tenant" }, Summary = "Get Tenant Config", Description = "Returns public tenant configuration (branding, theme, landing). No authentication required.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(object), Summary = "Tenant Configuration")]
    public async Task<HttpResponseData> GetTenantConfig(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenant/config")] HttpRequestData req)
    {
        var result = await _tenantBusiness.GetTenantConfig();

        if (!result.IsSuccess)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Tenant config not found.");
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(result.Value);
        return response;
    }

    [Function("ResolveTenant")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "tenant-resolve", tags: new[] { "Tenant" }, Summary = "Resolve Tenant by Host", Description = "Resolves tenant code and config from a hostname. No authentication required. First call the frontend makes.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("host", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Hostname to resolve (e.g. zerostour.com)")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(object), Summary = "Tenant code + config")]
    public async Task<HttpResponseData> ResolveTenant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenant/resolve")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var host = query["host"];

        if (string.IsNullOrWhiteSpace(host))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Query parameter 'host' is required.");
            return badRequest;
        }

        var result = await _tenantBusiness.ResolveTenantByHost(host);

        if (!result.IsSuccess)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Tenant not found for the given host.");
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(result.Value);
        return response;
    }
}
