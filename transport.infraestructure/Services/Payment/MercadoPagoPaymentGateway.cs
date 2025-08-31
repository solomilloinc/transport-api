using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using Transport.Business.Services.Payment;
using Transport.SharedKernel.Configuration;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Infraestructure.Services.Payment;

public class MercadoPagoPaymentGateway : IMercadoPagoPaymentGateway
{
    private readonly IMpIntegrationOption _mpIntegrationOption;

    public MercadoPagoPaymentGateway(IMpIntegrationOption mpIntegrationOption)
    {
        _mpIntegrationOption = mpIntegrationOption;
    }

    public async Task<MercadoPago.Resource.Payment.Payment> CreatePaymentAsync(PaymentCreateRequest request)
    {
        MercadoPagoConfig.AccessToken = _mpIntegrationOption.AccessToken;

        var client = new PaymentClient();
        return await client.CreateAsync(request);
    }

    public async Task<string> CreatePreferenceAsync(string externalReference, decimal totalAmount, List<PassengerReserveExternalCreateRequestDto> passengers)
    {
        MercadoPagoConfig.AccessToken = _mpIntegrationOption.AccessToken;

        var preferenceRequest = new PreferenceRequest
        {
            Items = passengers.Select((p, i) => new PreferenceItemRequest
            {
                Id = i.ToString(),
                Title = $"Pasaje de {p.FirstName} {p.LastName}",
                Quantity = 1,
                UnitPrice = p.Price,
                Description = $"Reserva {p.ReserveId}"
            }).ToList(),

            ExternalReference = externalReference,
            BackUrls = new PreferenceBackUrlsRequest
            {
                Success = _mpIntegrationOption.SuccessUrl,
                Failure = _mpIntegrationOption.FailureUrl,
                Pending = _mpIntegrationOption.PendingUrl
            },
            Purpose = "wallet_purchase",
            AutoReturn = "approved"
        };

        var client = new PreferenceClient();
        var preference = await client.CreateAsync(preferenceRequest);
        return preference.Id;
    }

    public async Task<MercadoPago.Resource.Payment.Payment> GetPaymentAsync(string paymentId)
    {
        MercadoPagoConfig.AccessToken = _mpIntegrationOption.AccessToken;
        var client = new PaymentClient();
        return await client.GetAsync(long.Parse(paymentId));
    }

}
