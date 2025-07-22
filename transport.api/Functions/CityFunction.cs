using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Transport.Infraestructure.Authorization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport_Api.Extensions;
using Transport.Domain.Cities.Abstraction;
using Transport.SharedKernel.Contracts.City;
using Transport_Api.Functions.Base;
using Microsoft.OpenApi.Models;
using Transport.SharedKernel;

namespace Transport_Api.Functions;

public sealed class CityFunction : FunctionBase
{
    private readonly ICityBusiness _CityBusiness;

    public CityFunction(ICityBusiness CityBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _CityBusiness = CityBusiness;
    }

    [Function("CreateCity")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "city-create", tags: new[] { "City" }, Summary = "Create new City", Description = "Creates a new city", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(CityCreateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "City Created")]
    public async Task<HttpResponseData> CreateCity(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "city-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<CityCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<CityCreateRequestDto>())
                          .BindAsync(_CityBusiness.Create);

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteCity")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "city-delete", tags: new[] { "City" }, Summary = "Delete City", Description = "Deletes a city", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("CityId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "City ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "City Deleted")]
    public async Task<HttpResponseData> DeleteCity(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "city-delete/{CityId:int}")] HttpRequestData req,
        int CityId)
    {
        var result = await _CityBusiness.Delete(CityId);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateCityStatus")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "city-updatestatus", tags: new[] { "City" }, Summary = "Update City Status", Description = "Updates the status of a city", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("CityId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "City ID")]
    [OpenApiParameter("status", In = ParameterLocation.Query, Required = true, Type = typeof(EntityStatusEnum), Description = "New status")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Status Updated")]
    public async Task<HttpResponseData> UpdateCityStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "city-status/{CityId:int}")] HttpRequestData req,
        int CityId)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var statusParsed = Enum.TryParse<EntityStatusEnum>(queryParams["status"], true, out var status);

        if (!statusParsed)
            throw new ArgumentException("Invalid status value");

        var result = await _CityBusiness.UpdateStatus(CityId, status);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateCity")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "city-update", tags: new[] { "City" }, Summary = "Update City", Description = "Updates an existing city", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("CityId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "City ID")]
    [OpenApiRequestBody("application/json", typeof(CityUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "City Updated")]
    public async Task<HttpResponseData> UpdateCity(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "city-update/{CityId:int}")] HttpRequestData req,
        int CityId)
    {
        var dto = await req.ReadFromJsonAsync<CityUpdateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<CityUpdateRequestDto>())
                          .BindAsync(x => _CityBusiness.Update(CityId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("GetCityReport")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "city-report", tags: new[] { "City" }, Summary = "Get City Report", Description = "Returns paginated list of Citys", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<CityReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<CityReportResponseDto>), Summary = "City Report")]
    public async Task<HttpResponseData> GetCityReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "city-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<CityReportFilterRequestDto>>();
        var result = await _CityBusiness.GetReport(filter);
        return await MatchResultAsync(req, result);
    }
}
