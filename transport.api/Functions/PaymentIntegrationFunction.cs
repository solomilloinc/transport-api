using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Transport.Domain.Reserves.Abstraction;
using Transport.SharedKernel.Configuration;

public class PaymentIntegrationFunction
{
    private readonly ILogger _logger;
    private readonly IReserveBusiness _reserveBusiness;
    private readonly IMpIntegrationOption _mpIntegrationOption;
    private readonly string _mpWebhookSecret;

    public PaymentIntegrationFunction(ILogger<PaymentIntegrationFunction> logger,
        IReserveBusiness reserveBusiness, 
        IMpIntegrationOption mpIntegrationOption)
    {
        _logger = logger;
        _mpIntegrationOption = mpIntegrationOption;
        _reserveBusiness = reserveBusiness;
    }

    [Function("MPWebhook")]
    public async Task<HttpResponseData> Run(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mp-webhook")] HttpRequestData req)
    {
        // Leer el cuerpo de la solicitud  
        var body = await req.ReadAsStringAsync();

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var dataId = query["data.id"]; // data.id debe venir como query param  

        req.Headers.TryGetValues("x-signature", out var sigs);
        req.Headers.TryGetValues("x-request-id", out var requestIds);

        var signature = sigs?.FirstOrDefault();
        var requestId = requestIds?.FirstOrDefault();

        // Verificamos firma HMAC-SHA256  
        if (!IsValidMercadoPagoHmacSignature(req, signature, requestId, dataId, _mpIntegrationOption.WebhookSecret))
        {
            _logger.LogWarning("Firma inválida de MercadoPago");
            var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteStringAsync("Firma inválida.");
            return unauthorized;
        }

        // Parseamos el evento  
        var payload = JObject.Parse(body);
        string? type = payload["type"]?.ToString();
        string? id = payload["data"]?["id"]?.ToString();

        if (type == "payment" && !string.IsNullOrEmpty(id))
        {
            await _reserveBusiness.UpdateReservePaymentsByExternalId(id);
            _logger.LogInformation("Evento de pago recibido: ID={id}, Tipo={type}", id, type);           

            var successResp = req.CreateResponse(HttpStatusCode.OK);
            await successResp.WriteStringAsync("Webhook procesado correctamente.");
            return successResp;
        }

        var invalidResp = req.CreateResponse(HttpStatusCode.BadRequest);
        await invalidResp.WriteStringAsync("Evento no manejado o sin ID.");
        return invalidResp;
    }

    private bool IsValidMercadoPagoHmacSignature(HttpRequestData req, string? signatureHeader, string? requestIdHeader, string? dataIdQuery, string secret)
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

        // Armar el string en el formato correcto
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
