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

    [Function("GetReserveReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "reserve-report", tags: new[] { "Reserve" }, Summary = "Get Reserve Report", Description = "Returns paginated list of reserve prices", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<ReserveReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<ReserveReportResponseDto>), Summary = "Reserve Report")]
    public async Task<HttpResponseData> GetReserveReport(
     [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reserve-price-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<ReserveReportFilterRequestDto>>();
        var result = await _reserveBusiness.GetReserveReport(filter);
        return await MatchResultAsync(req, result);
    }

}