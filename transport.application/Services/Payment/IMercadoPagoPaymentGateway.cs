using MercadoPago.Client.Payment;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.Services.Payment;

public interface IMercadoPagoPaymentGateway
{
    Task<MercadoPago.Resource.Payment.Payment> CreatePaymentAsync(PaymentCreateRequest request);
    Task<string> CreatePreferenceAsync(string externalReference, decimal totalAmount, List<PassengerReserveCreateRequestDto> passengers);
    Task<MercadoPago.Resource.Payment.Payment> GetPaymentAsync(string paymentId);
}
