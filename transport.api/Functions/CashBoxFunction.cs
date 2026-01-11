using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Transport.Infraestructure.Authorization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport_Api.Functions.Base;
using Microsoft.OpenApi.Models;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.CashBox;
using Transport.Domain.CashBoxes.Abstraction;

namespace Transport_Api.Functions;

public sealed class CashBoxFunction : FunctionBase
{
    private readonly ICashBoxBusiness _cashBoxBusiness;

    public CashBoxFunction(ICashBoxBusiness cashBoxBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _cashBoxBusiness = cashBoxBusiness;
    }

    [Function("CloseCashBox")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "cashbox-close", tags: new[] { "CashBox" }, Summary = "Cerrar caja y abrir nueva", Description = "Cierra la caja actual y abre una nueva automáticamente. No se puede cerrar si hay pagos pendientes.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(CloseCashBoxRequestDto), Required = true, Description = "Descripción para la nueva caja")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(CashBoxResponseDto), Summary = "Nueva caja abierta")]
    public async Task<HttpResponseData> CloseCashBox(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cashbox/close")] HttpRequestData req)
    {
        var request = await req.ReadFromJsonAsync<CloseCashBoxRequestDto>();
        var result = await _cashBoxBusiness.CloseCashBox(request!);
        return await MatchResultAsync(req, result);
    }

    [Function("GetCurrentCashBox")]
    [Authorize(["Admin", "User"])]
    [OpenApiOperation(operationId: "cashbox-current", tags: new[] { "CashBox" }, Summary = "Obtener caja actual", Description = "Obtiene la caja abierta actualmente.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(CashBoxResponseDto), Summary = "Caja actual")]
    public async Task<HttpResponseData> GetCurrentCashBox(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cashbox/current")] HttpRequestData req)
    {
        var result = await _cashBoxBusiness.GetCurrentCashBox();
        return await MatchResultAsync(req, result);
    }

    [Function("GetCashBoxReport")]
    [Authorize(["Admin", "User"])]
    [OpenApiOperation(operationId: "cashbox-report", tags: new[] { "CashBox" }, Summary = "Reporte de cajas", Description = "Obtiene un listado paginado de cajas.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<CashBoxReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<CashBoxResponseDto>), Summary = "Reporte de cajas")]
    public async Task<HttpResponseData> GetCashBoxReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cashbox/report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<CashBoxReportFilterRequestDto>>();
        var result = await _cashBoxBusiness.GetCashBoxReport(filter);
        return await MatchResultAsync(req, result);
    }
}
