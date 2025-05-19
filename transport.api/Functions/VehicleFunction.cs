using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Transport.Infraestructure.Authorization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport_Api.Extensions;
using Transport.Domain.Vehicles.Abstraction;
using Transport.SharedKernel.Contracts.Vehicle;
using Transport_Api.Functions.Base;
using Microsoft.OpenApi.Models;
using Transport.SharedKernel;

namespace Transport_Api.Functions;

public sealed class VehicleFunction : FunctionBase
{
    private readonly IVehicleBusiness _vehicleBusiness;

    public VehicleFunction(IVehicleBusiness vehicleBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _vehicleBusiness = vehicleBusiness;
    }

    [Function("CreateVehicle")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "vehicle-create", tags: new[] { "Vehicle" }, Summary = "Create new Vehicle", Description = "Creates a new vehicle", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(VehicleCreateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "Vehicle Created")]
    public async Task<HttpResponseData> CreateVehicle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "vehicle-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<VehicleCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<VehicleCreateRequestDto>())
                          .BindAsync(_vehicleBusiness.Create);

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteVehicle")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "vehicle-delete", tags: new[] { "Vehicle" }, Summary = "Delete Vehicle", Description = "Deletes a vehicle", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("vehicleId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Vehicle ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Vehicle Deleted")]
    public async Task<HttpResponseData> DeleteVehicle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "vehicle-delete/{vehicleId:int}")] HttpRequestData req,
        int vehicleId)
    {
        var result = await _vehicleBusiness.Delete(vehicleId);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateVehicleStatus")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "vehicle-updatestatus", tags: new[] { "Vehicle" }, Summary = "Update Vehicle Status", Description = "Updates the status of a vehicle", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("vehicleId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Vehicle ID")]
    [OpenApiParameter("status", In = ParameterLocation.Query, Required = true, Type = typeof(EntityStatusEnum), Description = "New status")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Status Updated")]
    public async Task<HttpResponseData> UpdateVehicleStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "vehicle-status/{vehicleId:int}")] HttpRequestData req,
        int vehicleId)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var statusParsed = Enum.TryParse<EntityStatusEnum>(queryParams["status"], true, out var status);

        if (!statusParsed)
            throw new ArgumentException("Invalid status value");

        var result = await _vehicleBusiness.UpdateStatus(vehicleId, status);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateVehicle")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "vehicle-update", tags: new[] { "Vehicle" }, Summary = "Update Vehicle", Description = "Updates an existing vehicle", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("vehicleId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Vehicle ID")]
    [OpenApiRequestBody("application/json", typeof(VehicleUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Vehicle Updated")]
    public async Task<HttpResponseData> UpdateVehicle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "vehicle-update/{vehicleId:int}")] HttpRequestData req,
        int vehicleId)
    {
        var dto = await req.ReadFromJsonAsync<VehicleUpdateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<VehicleUpdateRequestDto>())
                          .BindAsync(x => _vehicleBusiness.Update(vehicleId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("GetVehicleReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "vehicle-report", tags: new[] { "Vehicle" }, Summary = "Get Vehicle Report", Description = "Returns paginated list of vehicles", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<VehicleReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<VehicleReportResponseDto>), Summary = "Vehicle Report")]
    public async Task<HttpResponseData> GetVehicleReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "vehicle-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<VehicleReportFilterRequestDto>>();
        var result = await _vehicleBusiness.GetVehicleReport(filter);
        return await MatchResultAsync(req, result);
    }
}
