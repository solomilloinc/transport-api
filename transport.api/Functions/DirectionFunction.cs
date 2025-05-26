using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using System.Net;
using Transport.Domain.Directions.Abstraction;
using Transport.SharedKernel.Contracts.Direction;
using Transport.SharedKernel;
using Transport_Api.Functions.Base;
using Transport.Infraestructure.Authorization;
using Transport_Api.Extensions;
using Transport.SharedKernel.Contracts.City;

namespace transport_api.Functions;

public sealed class DirectionFunction : FunctionBase
{
    private readonly IDirectionBusiness _directionBusiness;

    public DirectionFunction(IDirectionBusiness directionBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _directionBusiness = directionBusiness;
    }

    [Function("CreateDirection")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "direction-create", tags: new[] { "Direction" }, Summary = "Create new Direction", Description = "Creates a new direction", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(DirectionCreateDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "Direction Created")]
    public async Task<HttpResponseData> CreateDirection(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "direction-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<DirectionCreateDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<DirectionCreateDto>())
            .BindAsync(_directionBusiness.CreateAsync);

        return await MatchResultAsync(req, result);
    }

    [Function("UpdateDirection")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "direction-update", tags: new[] { "Direction" }, Summary = "Update Direction", Description = "Updates an existing direction", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(DirectionUpdateDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Direction Updated")]
    public async Task<HttpResponseData> UpdateDirection(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "direction-update/{directionId:int}")] HttpRequestData req, int directionId)
    {
        var dto = await req.ReadFromJsonAsync<DirectionUpdateDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<DirectionUpdateDto>())
            .BindAsync(d => _directionBusiness.UpdateAsync(directionId, dto));

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteDirection")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "direction-delete", tags: new[] { "Direction" }, Summary = "Delete Direction", Description = "Deletes a direction", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("directionId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Direction ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Direction Deleted")]
    public async Task<HttpResponseData> DeleteDirection(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "direction-delete/{directionId:int}")] HttpRequestData req,
        int directionId)
    {
        var result = await _directionBusiness.DeleteAsync(directionId);
        return await MatchResultAsync(req, result);
    }

    [Function("GetDirectionReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "direction-report", tags: new[] { "Direction" }, Summary = "Get Direction Report", Description = "Returns paginated list of Directions", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<DirectionReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<DirectionReportDto>), Summary = "Direction Report")]
    public async Task<HttpResponseData> GetDirectionReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "direction-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<DirectionReportFilterRequestDto>>();
        var result = await _directionBusiness.GetReportAsync(filter);
        return await MatchResultAsync(req, result);
    }
}