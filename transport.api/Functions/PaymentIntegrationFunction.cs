using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Business.Services.Payment;
using Transport.Domain.Reserves.Abstraction;
using Transport.Infraestructure.Authentication;
using Transport.SharedKernel.Configuration;
using Transport.SharedKernel.Contracts.Reserve;

public class PaymentIntegrationFunction
{
    private readonly ILogger _logger;
    private readonly IReserveBusiness _reserveBusiness;
    private readonly IMpIntegrationOption _mpIntegrationOption;
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IMercadoPagoPaymentGateway _paymentGateway;

    public PaymentIntegrationFunction(ILogger<PaymentIntegrationFunction> logger,
        IReserveBusiness reserveBusiness,
        IMpIntegrationOption mpIntegrationOption,
        IApplicationDbContext dbContext,
        ITenantContext tenantContext,
        IMercadoPagoPaymentGateway paymentGateway)
    {
        _logger = logger;
        _mpIntegrationOption = mpIntegrationOption;
        _reserveBusiness = reserveBusiness;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _paymentGateway = paymentGateway;
    }

    [Function("MPWebhook")]
    public async Task<HttpResponseData> Run(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mp-webhook")] HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var dataId = query["data.id"];

        req.Headers.TryGetValues("x-signature", out var sigs);
        req.Headers.TryGetValues("x-request-id", out var requestIds);

        var signature = sigs?.FirstOrDefault();
        var requestId = requestIds?.FirstOrDefault();

        // Step 1: Validate HMAC with global webhook secret
        if (!IsValidMercadoPagoHmacSignature(signature, requestId, dataId, _mpIntegrationOption.WebhookSecret))
        {
            _logger.LogWarning("Invalid MercadoPago signature");
            var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteStringAsync("Firma inválida.");
            return unauthorized;
        }

        var payload = JObject.Parse(body!);
        string? type = payload["type"]?.ToString();
        string? id = payload["data"]?["id"]?.ToString();

        if (type != "payment" || string.IsNullOrEmpty(id))
        {
            var invalidResp = req.CreateResponse(HttpStatusCode.BadRequest);
            await invalidResp.WriteStringAsync("Evento no manejado o sin ID.");
            return invalidResp;
        }

        // Step 2: Call MP API to get payment details (uses global/fallback AccessToken since no tenant yet)
        var mpPayment = await _paymentGateway.GetPaymentAsync(id);

        if (mpPayment.Status == "in_process" || mpPayment.Status == "pending")
        {
            var okResp = req.CreateResponse(HttpStatusCode.OK);
            await okResp.WriteStringAsync("Payment in process.");
            return okResp;
        }

        // Step 3: Resolve tenant from ReservePayment using IgnoreQueryFilters
        // ExternalReference = ReservePaymentId
        if (!int.TryParse(mpPayment.ExternalReference, out var reservePaymentId))
        {
            _logger.LogWarning("Invalid ExternalReference in MP payment: {ExternalReference}", mpPayment.ExternalReference);
            var badResp = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResp.WriteStringAsync("ExternalReference inválido.");
            return badResp;
        }

        var reservePayment = await _dbContext.ReservePayments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(rp => rp.ReservePaymentId == reservePaymentId);

        if (reservePayment is null)
        {
            _logger.LogWarning("ReservePayment not found for ExternalReference: {ReservePaymentId}", reservePaymentId);
            var notFoundResp = req.CreateResponse(HttpStatusCode.BadRequest);
            await notFoundResp.WriteStringAsync("Pago no encontrado.");
            return notFoundResp;
        }

        // Step 4: Set tenant context
        if (_tenantContext is TenantContext tc)
        {
            tc.TenantId = reservePayment.TenantId;
        }

        // Step 5: Process payment with pre-fetched MP payment (avoids double API call)
        var externalPayment = new ExternalPaymentResultDto(
            PaymentExternalId: mpPayment.Id,
            ExternalReference: mpPayment.ExternalReference,
            Status: mpPayment.Status,
            StatusDetail: mpPayment.StatusDetail,
            RawJson: JsonConvert.SerializeObject(mpPayment)
        );
        var result = await _reserveBusiness.ProcessPaymentFromWebhook(externalPayment);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Payment webhook processed: ID={PaymentId}, TenantId={TenantId}", id, reservePayment.TenantId);
            var successResp = req.CreateResponse(HttpStatusCode.OK);
            await successResp.WriteStringAsync("Webhook procesado correctamente.");
            return successResp;
        }

        _logger.LogError("Payment webhook processing failed: {Error}", result.Error);
        var errorResp = req.CreateResponse(HttpStatusCode.InternalServerError);
        await errorResp.WriteStringAsync("Error procesando webhook.");
        return errorResp;
    }

    [Function("WalletForSuccess")]
    [OpenApiOperation(operationId: "wallet-for-success", tags: new[] { "Reserves" })]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Future reserves generated successfully")]
    public async Task<HttpResponseData> WalletForSuccess(
     [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("El Pago por wallet salió ok.");

        return response;
    }

    private static bool IsValidMercadoPagoHmacSignature(string? signatureHeader, string? requestIdHeader, string? dataIdQuery, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(requestIdHeader) || string.IsNullOrWhiteSpace(dataIdQuery))
            return false;

        var parts = signatureHeader.Split(',');
        string? ts = null;
        string? v1 = null;

        foreach (var part in parts)
        {
            var kv = part.Split('=');
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "ts") ts = kv[1].Trim();
            if (kv[0].Trim() == "v1") v1 = kv[1].Trim();
        }

        if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(v1)) return false;

        var manifest = $"id:{dataIdQuery.ToLowerInvariant()};request-id:{requestIdHeader};ts:{ts};";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));
        var computedHashHex = BitConverter.ToString(computedHash).Replace("-", "").ToLowerInvariant();

        return computedHashHex == v1.ToLowerInvariant();
    }
}

public record CreatePreferenceRequestDto(
    string ExternalReference,
    List<MercadoPagoItemDto> Items
);

public record MercadoPagoItemDto(
    string Id,
    string Title,
    string Description,
    int Quantity,
    decimal UnitPrice
);
