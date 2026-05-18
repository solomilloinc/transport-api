using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using System.Net;
using Transport.Domain.FrequentSubscriptions.Abstraction;
using Transport.Infraestructure.Authorization;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.FrequentSubscription;
using Transport_Api.Extensions;
using Transport_Api.Functions.Base;

namespace Transport_Api.Functions;

public sealed class FrequentSubscriptionsFunction : FunctionBase
{
    private readonly IFrequentSubscriptionBusiness _business;

    public FrequentSubscriptionsFunction(IFrequentSubscriptionBusiness business, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _business = business;
    }

    [Function("CreateFrequentSubscription")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "frequent-subscription-create", tags: new[] { "FrequentSubscription" }, Summary = "Crear suscripción frecuente", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(FrequentSubscriptionCreateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "FrequentSubscriptionId creado")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "frequent-subscription-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<FrequentSubscriptionCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<FrequentSubscriptionCreateRequestDto>())
                          .BindAsync(_business.Create);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateFrequentSubscription")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "frequent-subscription-update", tags: new[] { "FrequentSubscription" }, Summary = "Actualizar suscripción frecuente", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("id", In = ParameterLocation.Path, Required = true, Type = typeof(int))]
    [OpenApiRequestBody("application/json", typeof(FrequentSubscriptionUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Actualizada")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "frequent-subscription-update/{id:int}")] HttpRequestData req,
        int id)
    {
        var dto = await req.ReadFromJsonAsync<FrequentSubscriptionUpdateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<FrequentSubscriptionUpdateRequestDto>())
                          .BindAsync(x => _business.Update(id, x));
        return await MatchResultAsync(req, result);
    }

    [Function("CancelFrequentSubscription")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "frequent-subscription-cancel", tags: new[] { "FrequentSubscription" }, Summary = "Cancelar (cascade) suscripción frecuente", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("id", In = ParameterLocation.Path, Required = true, Type = typeof(int))]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Cancelada con cascade")]
    public async Task<HttpResponseData> Cancel(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "frequent-subscription-cancel/{id:int}")] HttpRequestData req,
        int id)
    {
        var result = await _business.Cancel(id);
        return await MatchResultAsync(req, result);
    }

    [Function("GetFrequentSubscriptionCancelPreview")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "frequent-subscription-cancel-preview", tags: new[] { "FrequentSubscription" }, Summary = "Preview del cancel: cuántos pasajeros se cancelarían y cuánto se reembolsaría", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("id", In = ParameterLocation.Path, Required = true, Type = typeof(int))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(FrequentSubscriptionCancelPreviewDto), Summary = "Preview")]
    public async Task<HttpResponseData> GetCancelPreview(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "frequent-subscription/{id:int}/cancel-preview")] HttpRequestData req,
        int id)
    {
        var result = await _business.GetCancelPreview(id);
        return await MatchResultAsync(req, result);
    }

    [Function("GetFrequentSubscriptionById")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "frequent-subscription-get-by-id", tags: new[] { "FrequentSubscription" }, Summary = "Detalle de suscripción frecuente", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("id", In = ParameterLocation.Path, Required = true, Type = typeof(int))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(FrequentSubscriptionResponseDto), Summary = "Suscripción")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "frequent-subscription/{id:int}")] HttpRequestData req,
        int id)
    {
        var result = await _business.GetById(id);
        return await MatchResultAsync(req, result);
    }

    [Function("GetFrequentSubscriptionReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "frequent-subscription-report", tags: new[] { "FrequentSubscription" }, Summary = "Reporte paginado de suscripciones frecuentes", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<FrequentSubscriptionReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<FrequentSubscriptionResponseDto>), Summary = "Reporte")]
    public async Task<HttpResponseData> GetReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "frequent-subscription-report")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<PagedReportRequestDto<FrequentSubscriptionReportFilterRequestDto>>();
        var result = await _business.GetReport(dto);
        return await MatchResultAsync(req, result);
    }
}
