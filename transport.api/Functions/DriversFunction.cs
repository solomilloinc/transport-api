using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Transport.Infraestructure.Authorization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport_Api.Extensions;
using Transport.Domain.Drivers.Abstraction;
using Transport.SharedKernel.Contracts.Driver;
using Transport_Api.Functions.Base;
using Microsoft.OpenApi.Models;
using Transport.SharedKernel;

namespace Transport_Api.Functions;
public sealed class DriversFunction : FunctionBase
{
    private readonly IDriverBusiness _driverBusiness;

    public DriversFunction(IDriverBusiness driverBusiness, IServiceProvider serviceProvider) :
        base(serviceProvider)
    {
        _driverBusiness = driverBusiness;
    }

    [Function("CreateDriver")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "driver-create", tags: new[] { "Driver" }, Summary = "Create new Driver", Description = "Creates a new driver", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(DriverCreateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(DriverCreateRequestDto), Summary = "Driver Created")]
    public async Task<HttpResponseData> CreateDriver(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "driver-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<DriverCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<DriverCreateRequestDto>())
                          .BindAsync(_driverBusiness.Create);

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteDriver")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "driver-delete", tags: new[] { "Driver" }, Summary = "Delete Driver", Description = "Deletes a driver", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("driverId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Driver ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Driver Deleted")]
    public async Task<HttpResponseData> DeleteDriver(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "driver-delete/{driverId:int}")] HttpRequestData req,
        int driverId)
    {
        var result = await _driverBusiness.Delete(driverId);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateDriverStatus")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "driver-updatestatus", tags: new[] { "Driver" }, Summary = "Update Driver Status", Description = "Updates the status of a driver", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("driverId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Driver ID")]
    [OpenApiParameter("status", In = ParameterLocation.Query, Required = true, Type = typeof(EntityStatusEnum), Description = "New status")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Status Updated")]
    public async Task<HttpResponseData> UpdateDriverStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "driver-status/{driverId:int}")] HttpRequestData req,
        int driverId)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var statusParsed = Enum.TryParse<EntityStatusEnum>(queryParams["status"], true, out var status);

        if (!statusParsed)
            throw new ArgumentException("Invalid status value");

        var result = await _driverBusiness.UpdateStatus(driverId, status);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateDriver")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "driver-update", tags: new[] { "Driver" }, Summary = "Update Driver", Description = "Updates an existing driver", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("driverId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Driver ID")]
    [OpenApiRequestBody("application/json", typeof(DriverUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Driver Updated")]
    public async Task<HttpResponseData> UpdateDriver(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "driver-update/{driverId:int}")] HttpRequestData req,
        int driverId)
    {
        var dto = await req.ReadFromJsonAsync<DriverUpdateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<DriverUpdateRequestDto>())
                          .BindAsync(x => _driverBusiness.Update(driverId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("GetDriverReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "driver-report", tags: new[] { "Driver" }, Summary = "Get Driver Report", Description = "Returns paginated list of drivers", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<DriverReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Transport.SharedKernel.PagedReportResponseDto<DriverReportResponseDto>), Summary = "Driver Report")]
    public async Task<HttpResponseData> GetDriverReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "driver-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<DriverReportFilterRequestDto>>();
        var result = await _driverBusiness.GetDriverReport(filter);
        return await MatchResultAsync(req, result);
    }
}
