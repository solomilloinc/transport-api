using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Transport.SharedKernel.Contracts.Payment; // Asegurate de tener este DTO creado

public class PaymentIntegrationFunction
{
    private readonly ILogger _logger;

    public PaymentIntegrationFunction(ILogger<PaymentIntegrationFunction> logger)
    {
        _logger = logger;
    }

    [Function("CreatePreference")]
    public async Task<HttpResponseData> CreatePreference(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mp-create-preference")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<CreatePreferenceRequestDto>();

        // Validación básica del DTO (podés agregar FluentValidation o similares)
        if (dto is null || dto.Items is null || !dto.Items.Any())
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Datos inválidos o incompletos.");
            return badResponse;
        }

        // Armar JSON para Mercado Pago
        var mpPayload = new
        {
            items = dto.Items.Select(x => new
            {
                id = x.Id,
                title = x.Title,
                description = x.Description,
                quantity = x.Quantity,
                unit_price = x.UnitPrice
            }),
            external_reference = dto.ExternalReference,
            purpose = "wallet_purchase"
        };

        var json = JsonConvert.SerializeObject(mpPayload, new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        });
        
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/checkout/preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorResp = req.CreateResponse(response.StatusCode);
            await errorResp.WriteStringAsync(responseContent);
            return errorResp;
        }

        var mpResponse = JsonConvert.DeserializeObject<JObject>(responseContent);
        var preferenceId = mpResponse?["id"]?.ToString();

        var successResp = req.CreateResponse(HttpStatusCode.OK);
        await successResp.WriteAsJsonAsync(new { preference_id = preferenceId });
        return successResp;
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
        string secret = "";

        // Verificamos firma HMAC-SHA256  
        if (!IsValidMercadoPagoHmacSignature(req, signature, requestId, dataId, secret))
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
            string accessToken = "";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var mpResponse = await client.GetAsync($"https://api.mercadopago.com/v1/payments/{id}");
            string mpJson = await mpResponse.Content.ReadAsStringAsync();

            _logger.LogInformation("Respuesta completa de MP: {json}", mpJson);

            if (!mpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Error consultando pago: {json}", mpJson);
                var failResp = req.CreateResponse(HttpStatusCode.BadRequest);
                await failResp.WriteStringAsync("Error consultando pago.");
                return failResp;
            }

            var paymentData = JObject.Parse(mpJson);
            string? externalRef = paymentData["external_reference"]?.ToString();
            string? status = paymentData["status"]?.ToString();

            _logger.LogInformation("Pago recibido: ID={id}, Estado={status}, Ref={ref}", id, status, externalRef);

            // ⚠️ Acá hacés tu lógica de negocio  
            // Ej: await _paymentService.MarkReservationAsPaid(externalRef, status);  

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
