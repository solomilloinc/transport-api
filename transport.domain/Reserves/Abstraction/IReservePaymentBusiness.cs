using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Payment;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Reserves.Abstraction;

public interface IReservePaymentBusiness
{
    Task<Result<bool>> CreatePaymentsAsync(
        int customerId,
        int reserveId,
        List<CreatePaymentRequestDto> payments);

    Task<Result<bool>> SettleCustomerDebtAsync(SettleCustomerDebtRequestDto request);

    Task<Result<bool>> UpdateReservePaymentsByExternalId(string externalPaymentId);

    Task<Result<bool>> ProcessPaymentFromWebhook(ExternalPaymentResultDto externalPayment);
}
