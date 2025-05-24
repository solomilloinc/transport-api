using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Transport.SharedKernel.Contracts.Reserve;
using Transport.SharedKernel;
using Transport.Infraestructure.Authorization;
using Transport_Api.Functions.Base;
using Transport.Domain.Reserves.Abstraction;
using System.Globalization;
using Microsoft.OpenApi.Models;
using Transport.SharedKernel.Contracts.Vehicle;
using Transport_Api.Extensions;
using Transport.Business.ReserveBusiness.Validation;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.Json;
using Transport.SharedKernel.Contracts.Customer;

namespace transport_api.Functions;

public class ReservesFunction : FunctionBase
{
    private readonly IReserveBusiness _reserveBusiness;

    public ReservesFunction(IReserveBusiness reserveBusiness, IServiceProvider serviceProvider)
       : base(serviceProvider)
    {
        _reserveBusiness = reserveBusiness;
    }

    [Function("CreatePassengerReserves")]
    [Authorize("Admin")]
    [OpenApiOperation(
    operationId: "passenger-reserves-create",
    tags: new[] { "Reserve" },
    Summary = "Create Passenger Reserves",
    Description = "Creates customer reserves for passengers, creating customers if needed.",
    Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(CustomerReserveCreateRequestWrapperValidator), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Result<bool>), Summary = "Passenger reserves created successfully.")]
    public async Task<HttpResponseData> CreatePassengerReserves(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "passenger-reserves-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<CustomerReserveCreateRequestWrapperDto>();

        var result = await ValidateAndMatchAsync(req, dto, GetValidator<CustomerReserveCreateRequestWrapperDto>())
                        .BindAsync(_reserveBusiness.CreatePassengerReserves);

        return await MatchResultAsync(req, result);
    }


    [Function("GetReservePriceReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "reserve-price-report", tags: new[] { "ReservePrice" }, Summary = "Get Reserve Price Report", Description = "Returns paginated list of reserve prices", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<ReservePriceReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<ReservePriceReportResponseDto>), Summary = "Reserve Price Report")]
    public async Task<HttpResponseData> GetReservePriceReport(
     [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reserve-price-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<ReservePriceReportFilterRequestDto>>();
        var result = await _reserveBusiness.GetReservePriceReport(filter);
        return await MatchResultAsync(req, result);
    }

    [Function("GetReserveReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "reserve-report", tags: new[] { "Reserve" }, Summary = "Get Reserve Report", Description = "Returns paginated list of reserve", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<ReserveReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<ReserveReportResponseDto>), Summary = "Reserve Report")]
    public async Task<HttpResponseData> GetReserveReport(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reserve-report/{reserveDate}")] HttpRequestData req, string reserveDate)
    {
        if (!DateTime.TryParseExact(reserveDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return req.CreateResponse(HttpStatusCode.BadRequest);

        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<ReserveReportFilterRequestDto>>();
        var result = await _reserveBusiness.GetReserveReport(parsedDate, filter);
        return await MatchResultAsync(req, result);
    }


    [Function("GetCustomerReserveReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "customer-reserve-report", tags: new[] { "Reserve" }, Summary = "Get Customer Reserves Report", Description = "Returns paginated list of customer reserves", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<CustomerReserveReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<CustomerReserveReportResponseDto>), Summary = "Customer Reserve Report")]
    public async Task<HttpResponseData> GetCustomerReserveReport(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customer-reserve-report/{reserveId:int}")] HttpRequestData req,
    int reserveId)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<CustomerReserveReportFilterRequestDto>>();
        var result = await _reserveBusiness.GetReserveCustomerReport(reserveId, filter);
        return await MatchResultAsync(req, result);
    }

}