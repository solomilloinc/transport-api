using MercadoPago.Client.Payment;
using MercadoPago.Config;
using Transport.Business.Services.Payment;
using Transport.SharedKernel.Configuration;

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
}
