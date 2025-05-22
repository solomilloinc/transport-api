using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport.Domain.Payments.Abstraction;
using Transport.SharedKernel.Contracts.Payment;
using Transport_Api.Functions.Base;
using Transport.Infraestructure.Authorization;
using Transport_Api.Extensions;

namespace Transport_Api.Functions;

public sealed class PaymentFunction : FunctionBase
{
    private readonly IPaymentBusiness _paymentBusiness;

    public PaymentFunction(IPaymentBusiness paymentBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _paymentBusiness = paymentBusiness;
    }

    [Function("CreatePayment")]
    //[Authorize("User")] // o "Anonymous" si no requiere token
    [OpenApiOperation(operationId: "payment-create", tags: new[] { "Payment" },
        Summary = "Create payment via card",
        Description = "Creates a new payment using card data",
        Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PaymentCreateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(bool), Summary = "Payment Created")]
    public async Task<HttpResponseData> CreatePayment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payment-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<PaymentCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<PaymentCreateRequestDto>())
                          .BindAsync(_paymentBusiness.CreatePayment);

        return await MatchResultAsync(req, result);
    }

    [Function("PaymentWebhookNotification")]
    public async Task<HttpResponseData> HandleWebhook(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payment-mp-webhook")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<WebhookNotification>();

        var result = await ValidateAndMatchAsync(req, dto, GetValidator<WebhookNotification>())
                          .BindAsync(_paymentBusiness.ProcessWebhookAsync);

        return await MatchResultAsync(req, result);
    }
}
