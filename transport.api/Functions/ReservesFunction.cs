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
using Transport.SharedKernel.Contracts.Passenger;

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
    [OpenApiRequestBody("application/json", typeof(PassengerReserveCreateRequestWrapperDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Result<bool>), Summary = "Passenger reserves created successfully.")]
    public async Task<HttpResponseData> CreatePassengerReserves(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "passenger-reserves-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<PassengerReserveCreateRequestWrapperDto>();

        var result = await ValidateAndMatchAsync(req, dto, GetValidator<PassengerReserveCreateRequestWrapperDto>())
                        .BindAsync(_reserveBusiness.CreatePassengerReserves);

        return await MatchResultAsync(req, result);
    }

    [Function("CreatePassengerReserveExternal")]
    [AllowAnonymous]
    [OpenApiOperation(
    operationId: "passenger-reserves-create",
    tags: new[] { "Reserve" },
    Summary = "Create Passenger Reserves external",
    Description = "Creates customer reserves for passengers, creating customers if needed in user final",
    Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PassengerReserveCreateRequestWrapperExternalDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Result<bool>), Summary = "Passenger reserves created successfully.")]
    public async Task<HttpResponseData> CreatePassengerReserveExternal(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "passenger-reserves-create-external")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<PassengerReserveCreateRequestWrapperExternalDto>();

        var result = await ValidateAndMatchAsync(req, dto, GetValidator<PassengerReserveCreateRequestWrapperExternalDto>())
                        .BindAsync(_reserveBusiness.CreatePassengerReservesExternal);

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

    [Function("GetPublicReserveSummary")]
    [AllowAnonymous]
    [OpenApiOperation(
    operationId: "public-reserve-summary",
    tags: new[] { "Reserve" },
    Summary = "Get Reserve Summary for Users",
    Description = "Returns a grouped (outbound/return) paginated list of available reserves for final users.",
    Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<ReserveReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ReserveGroupedPagedReportResponseDto), Summary = "Grouped Reserve Report")]
    public async Task<HttpResponseData> GetPublicReserveSummary(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "public/reserve-summary/")] HttpRequestData req,
    string reserveDate)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<ReserveReportFilterRequestDto>>();
        var result = await _reserveBusiness.GetReserveReport(filter);
        return await MatchResultAsync(req, result);
    }


    [Function("GetCustomerReserveReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "customer-reserve-report", tags: new[] { "Reserve" }, Summary = "Get Customer Reserves Report", Description = "Returns paginated list of customer reserves", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<PassengerReserveReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<PassengerReserveReportResponseDto>), Summary = "Customer Reserve Report")]
    public async Task<HttpResponseData> GetCustomerReserveReport(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customer-reserve-report/{reserveId:int}")] HttpRequestData req,
    int reserveId)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<PassengerReserveReportFilterRequestDto>>();
        var result = await _reserveBusiness.GetReservePassengerReport(reserveId, filter);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateReserve")]
    [Authorize("Admin")]
    [OpenApiOperation(
    operationId: "reserve-update",
    tags: new[] { "Reserve" },
    Summary = "Update a reserve",
    Description = "Updates a reserve with new data",
    Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(ReserveUpdateRequestDto), Required = true)]
    [OpenApiParameter(name: "reserveId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Summary = "The ID of the reserve to update")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Result<bool>), Summary = "Reserve updated successfully")]
    [OpenApiResponseWithoutBody(HttpStatusCode.NotFound, Summary = "Reserve not found")]
    public async Task<HttpResponseData> UpdateReserve(
    [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "reserve-update/{reserveId:int}")] HttpRequestData req,
    int reserveId)
    {
        var dto = await req.ReadFromJsonAsync<ReserveUpdateRequestDto>();

        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ReserveUpdateRequestDto>())
                        .BindAsync(request => _reserveBusiness.UpdateReserveAsync(reserveId, request));

        return await MatchResultAsync(req, result);
    }

    [Function("CreateReservePayments")]
    [Authorize("Admin")]
    [OpenApiOperation(
    operationId: "reserve-create-payments",
    tags: new[] { "ReservePayments" },
    Summary = "Create Payments for a Reserve",
    Description = "Creates one or more payments associated to a reserve and customer.",
    Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(List<CreatePaymentRequestDto>), Required = true)]
    [OpenApiParameter(name: "reserveId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Summary = "The ID of the reserve")]
    [OpenApiParameter(name: "customerId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Summary = "The ID of the customer")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Result<bool>), Summary = "Payments created successfully")]
    [OpenApiResponseWithoutBody(HttpStatusCode.BadRequest, Summary = "Invalid request")]
    public async Task<HttpResponseData> CreateReservePayments(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reserve-payments-create/{reserveId:int}/{customerId:int}")] HttpRequestData req,
    int reserveId,
    int customerId)
    {
        var payments = await req.ReadFromJsonAsync<List<CreatePaymentRequestDto>>();

        if (payments == null || !payments.Any())
            return req.CreateResponse(HttpStatusCode.BadRequest);

        var result = await _reserveBusiness.CreatePaymentsAsync(customerId, reserveId, payments);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateCustomerReserve")]
    [Authorize("Admin")]
    [OpenApiOperation(
    operationId: "customer-reserve-update",
    tags: new[] { "CustomerReserve" },
    Summary = "Update a Customer Reserve",
    Description = "Updates the specified customer reserve with new pickup/dropoff locations or travel status.",
    Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PassengerReserveUpdateRequestDto), Required = true)]
    [OpenApiParameter(name: "customerReserveId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Summary = "ID of the customer reserve")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Result<bool>), Summary = "Customer reserve updated successfully.")]
    [OpenApiResponseWithoutBody(HttpStatusCode.NotFound, Summary = "Customer reserve not found.")]
    public async Task<HttpResponseData> UpdateCustomerReserve(
    [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "customer-reserve-update/{customerReserveId:int}")] HttpRequestData req,
    int customerReserveId)
    {
        var dto = await req.ReadFromJsonAsync<PassengerReserveUpdateRequestDto>();

        var result = await ValidateAndMatchAsync(req, dto, GetValidator<PassengerReserveUpdateRequestDto>())
                        .BindAsync(update => _reserveBusiness.UpdatePassengerReserveAsync(customerReserveId, update));

        return await MatchResultAsync(req, result);
    }

}