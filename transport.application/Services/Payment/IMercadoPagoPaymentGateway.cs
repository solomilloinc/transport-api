using MercadoPago.Client.Payment;
using Transport.SharedKernel.Contracts.Payment;

namespace Transport.Business.Services.Payment;

public interface IMercadoPagoPaymentGateway
{
    Task<MercadoPago.Resource.Payment.Payment> CreatePaymentAsync(PaymentCreateRequest request);
    Task<string> CreatePreferenceAsync(string externalReference, decimal totalAmount, List<PaymentPreferenceItemDto> items);
    Task<MercadoPago.Resource.Payment.Payment> GetPaymentAsync(string paymentId);
}
