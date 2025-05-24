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

namespace transport_api.Functions;

public class ReservesFunction : FunctionBase
{
    private readonly IReserveBusiness _reserveBusiness;

    public ReservesFunction(IReserveBusiness reserveBusiness, IServiceProvider serviceProvider)
       : base(serviceProvider)
    {
        _reserveBusiness = reserveBusiness;
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
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reserve-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<ReserveReportFilterRequestDto>>();
        var result = await _reserveBusiness.GetReserveReport(filter);
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