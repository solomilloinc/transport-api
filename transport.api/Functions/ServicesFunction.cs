using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Transport.Infraestructure.Authorization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport_Api.Extensions;
using Transport_Api.Functions.Base;
using Transport.SharedKernel.Contracts.Service;
using Transport.Domain.Services.Abstraction;
using Microsoft.OpenApi.Models;
using Transport.SharedKernel;

namespace Transport_Api.Functions;

public sealed class ServicesFunction : FunctionBase
{
    private readonly IServiceBusiness _serviceBusiness;

    public ServicesFunction(IServiceBusiness serviceBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _serviceBusiness = serviceBusiness;
    }

    [Function("CreateService")]

    //[Authorize("Admin")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "service-create", tags: new[] { "Service" }, Summary = "Create Service", Description = "Creates a new service", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(ServiceCreateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "Service Created")]
    public async Task<HttpResponseData> CreateService(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "service-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<ServiceCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ServiceCreateRequestDto>())
                          .BindAsync(_serviceBusiness.Create);

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteService")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "service-delete", tags: new[] { "Service" }, Summary = "Delete Service", Description = "Deletes an existing service", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("serviceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Service Deleted")]
    public async Task<HttpResponseData> DeleteService(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "service-delete/{serviceId:int}")] HttpRequestData req,
        int serviceId)
    {
        var result = await _serviceBusiness.Delete(serviceId);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateService")]
    //[Authorize("Admin")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "service-update", tags: new[] { "Service" }, Summary = "Update Service", Description = "Updates an existing service", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("serviceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service ID")]
    [OpenApiRequestBody("application/json", typeof(ServiceUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Service Updated")]
    public async Task<HttpResponseData> UpdateService(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "service-update/{serviceId:int}")] HttpRequestData req,
        int serviceId)
    {
        var dto = await req.ReadFromJsonAsync<ServiceCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ServiceCreateRequestDto>())
                          .BindAsync(x => _serviceBusiness.Update(serviceId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("GetServiceReport")]
    //[Authorize("Admin")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "service-report", tags: new[] { "Service" }, Summary = "Get Service Report", Description = "Returns paginated list of services", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<ServiceReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<ServiceReportResponseDto>), Summary = "Service Report")]
    public async Task<HttpResponseData> GetServiceReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "service-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<ServiceReportFilterRequestDto>>();
        var result = await _serviceBusiness.GetServiceReport(filter);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdatePricesByPercentage")]
    //[Authorize("Admin")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "service-update-prices", tags: new[] { "Service" }, Summary = "Update Prices by Percentage", Description = "Performs a massive update of service prices based on a percentage", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PriceMassiveUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Prices Updated")]
    public async Task<HttpResponseData> UpdatePricesByPercentage(
    [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "service-update-prices")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<PriceMassiveUpdateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<PriceMassiveUpdateRequestDto>())
                          .BindAsync(_serviceBusiness.UpdatePricesByPercentageAsync);

        return await MatchResultAsync(req, result);
    }

    [Function("AddPrice")]
    //[Authorize("Admin")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "add-price", tags: new[] { "Service" }, Summary = "Add Price", Description = "Adds a price to a service", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("serviceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service ID")]
    [OpenApiRequestBody("application/json", typeof(ServicePriceAddDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Price Added")]
    public async Task<HttpResponseData> AddPrice(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "service/{serviceId:int}/price-add")] HttpRequestData req,
    int serviceId)
    {
        var dto = await req.ReadFromJsonAsync<ServicePriceAddDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ServicePriceAddDto>())
                        .BindAsync(x => _serviceBusiness.AddPrice(serviceId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("UpdatePrice")]
    //[Authorize("Admin")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "update-price", tags: new[] { "Service" }, Summary = "Update Price", Description = "Updates an existing service price", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("serviceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service ID")]
    [OpenApiRequestBody("application/json", typeof(ServicePriceUpdateDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Price Updated")]
    public async Task<HttpResponseData> UpdatePrice(
    [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "service/{serviceId:int}/price-update")] HttpRequestData req,
    int serviceId)
    {
        var dto = await req.ReadFromJsonAsync<ServicePriceUpdateDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ServicePriceUpdateDto>())
                        .BindAsync(x => _serviceBusiness.UpdatePrice(serviceId, x));

        return await MatchResultAsync(req, result);
    }

}
