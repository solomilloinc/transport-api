using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Transport.Infraestructure.Authorization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport_Api.Extensions;
using Transport.SharedKernel.Contracts.VehicleType;
using Transport_Api.Functions.Base;
using Microsoft.OpenApi.Models;
using Transport.SharedKernel;
using Transport.Domain.Vehicles.Abstraction;

namespace Transport_Api.Functions;

public sealed class VehicleTypeFunction : FunctionBase
{
    private readonly IVehicleTypeBusiness _vehicleTypeBusiness;

    public VehicleTypeFunction(IVehicleTypeBusiness vehicleTypeBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _vehicleTypeBusiness = vehicleTypeBusiness;
    }

    [Function("CreateVehicleType")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "vehicleType-create", tags: new[] { "VehicleType" }, Summary = "Create new Vehicle Type", Description = "Creates a new vehicle type", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(VehicleTypeCreateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "Vehicle Type Created")]
    public async Task<HttpResponseData> CreateVehicleType(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "vehicle-type-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<VehicleTypeCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<VehicleTypeCreateRequestDto>())
                          .BindAsync(_vehicleTypeBusiness.Create);

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteVehicleType")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "vehicleType-delete", tags: new[] { "VehicleType" }, Summary = "Delete Vehicle Type", Description = "Deletes a vehicle type", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("vehicleTypeId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Vehicle Type ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Vehicle Type Deleted")]
    public async Task<HttpResponseData> DeleteVehicleType(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "vehicle-type-delete/{vehicleTypeId:int}")] HttpRequestData req,
        int vehicleTypeId)
    {
        var result = await _vehicleTypeBusiness.Delete(vehicleTypeId);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateVehicleType")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "vehicleType-update", tags: new[] { "VehicleType" }, Summary = "Update Vehicle Type", Description = "Updates an existing vehicle type", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("vehicleTypeId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Vehicle Type ID")]
    [OpenApiRequestBody("application/json", typeof(VehicleTypeUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Vehicle Type Updated")]
    public async Task<HttpResponseData> UpdateVehicleType(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "vehicle-type-update/{vehicleTypeId:int}")] HttpRequestData req,
        int vehicleTypeId)
    {
        var dto = await req.ReadFromJsonAsync<VehicleTypeUpdateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<VehicleTypeUpdateRequestDto>())
                          .BindAsync(x => _vehicleTypeBusiness.Update(vehicleTypeId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("GetVehicleTypeReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "vehicleType-report", tags: new[] { "VehicleType" }, Summary = "Get Vehicle Type Report", Description = "Returns paginated list of vehicle types", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<VehicleTypeReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<VehicleTypeReportResponseDto>), Summary = "Vehicle Type Report")]
    public async Task<HttpResponseData> GetVehicleTypeReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "vehicle-type-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<VehicleTypeReportFilterRequestDto>>();
        var result = await _vehicleTypeBusiness.GetVehicleTypeReport(filter);
        return await MatchResultAsync(req, result);
    }
}
