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

    [OpenApiOperation(operationId: "driver", tags: new[] { "Driver" }, Summary = "Create new Driver", Description = "New Driver", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(DriverCreateRequestDto), Required = true, Description = "Create Driver")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(DriverCreateRequestDto), Summary = "Create Driver", Description = "test.")]

    public async Task<HttpResponseData> CreateDriver(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "driver-create")] HttpRequestData req)
    {
        var login = await req.ReadFromJsonAsync<DriverCreateRequestDto>();

        var result = await ValidateAndMatchAsync(req, login, GetValidator<DriverCreateRequestDto>())
                           .BindAsync(_driverBusiness.Create);

        return await MatchResultAsync(req, result);
    }
}
