using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Payment;

namespace Transport.Domain.Payments.Abstraction;

public interface IPaymentBusiness
{
    Task<Result<bool>> CreatePayment(PaymentCreateRequestDto payment);
    Task<Result<bool>> ProcessWebhookAsync(WebhookNotification notification);
}
