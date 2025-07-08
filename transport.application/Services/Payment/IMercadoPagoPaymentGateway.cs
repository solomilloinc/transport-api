using MercadoPago.Client.Payment;

namespace Transport.Business.Services.Payment;

public interface IMercadoPagoPaymentGateway
{
    Task<MercadoPago.Resource.Payment.Payment> CreatePaymentAsync(PaymentCreateRequest request);
}
