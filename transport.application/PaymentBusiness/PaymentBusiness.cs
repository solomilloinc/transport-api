using MercadoPago.Client.Common;
using MercadoPago.Client.Payment;
using MercadoPago.Config;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Transport.Business.Data;
using Transport.Domain.Payments;
using Transport.Domain.Payments.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Configuration;
using Transport.SharedKernel.Contracts.Payment;

namespace Transport.Business.PaymentBusiness;

public class PaymentBusiness : IPaymentBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly IMpIntegrationOption _mpIntegrationOption;

    public PaymentBusiness(IApplicationDbContext context, IMpIntegrationOption mpIntegrationOption)
    {
        _context = context;
        _mpIntegrationOption = mpIntegrationOption;
    }

    public async Task<Result<bool>> CreatePayment(PaymentCreateRequestDto paymentData)
    {
        MercadoPagoConfig.AccessToken = _mpIntegrationOption.AccessToken;

        var paymentRequest = new PaymentCreateRequest
        {
            TransactionAmount = paymentData.TransactionAmount,
            Token = paymentData.Token,
            Description = paymentData.Description,
            Installments = paymentData.Installments,
            PaymentMethodId = paymentData.PaymentMethodId,
            Payer = new PaymentPayerRequest
            {
                Email = "solomillo@test.com"
            }
        };

        var client = new PaymentClient();
        var result = await client.CreateAsync(paymentRequest);

        var payment = new Payment();
        MapPaymentData(payment, result, paymentData);
        _context.Payments.Add(payment);
        await _context.SaveChangesWithOutboxAsync();

        return true;
    }

    private decimal GetTotalRefundedAmount(MercadoPago.Resource.Payment.Payment payment)
    {
        return payment.Refunds?.Sum(r => r.Amount ?? 0m) ?? 0m;
    }

    public async Task<Result<bool>> ProcessWebhookAsync(WebhookNotification notification)
    {
        if (!long.TryParse(notification.Id, out var paymentMpId))
            return Result.Failure<bool>(Error.Failure("MpWebHook", "Invalid payment ID"));

        var client = new PaymentClient();
        var result = await client.GetAsync(paymentMpId);

        var existing = await _context.Payments
            .FirstOrDefaultAsync(p => p.PaymentMpId == result.Id);

        if (existing is null)
        {
            return Result.Failure<bool>(Error.Problem("MpWebHook", "Payment not found in system"));
        }

        MapPaymentData(existing, result);

        _context.Payments.Update(existing);
        await _context.SaveChangesWithOutboxAsync();

        return true;
    }

    private void MapPaymentData(Payment entity, MercadoPago.Resource.Payment.Payment source, PaymentCreateRequestDto? paymentData = null)
    {
        if (paymentData != null)
        {
            entity.Amount = paymentData.TransactionAmount;
            entity.Email = paymentData.Payer?.Email ?? entity.Email;
        }

        entity.PaymentMpId = source.Id;
        entity.RawJson = JsonSerializer.Serialize(source);
        entity.Status = source.Status.ToPaymentStatus();
        entity.StatusDetail = source.StatusDetail.ToPaymentStatusDetail();
        entity.ExternalReference = source.ExternalReference;
        entity.Currency = source.CurrencyId;
        entity.Installments = source.Installments;
        entity.PaymentMethodId = source.PaymentMethodId;
        entity.PaymentTypeId = source.PaymentTypeId;
        entity.CardLastFourDigits = source.Card?.LastFourDigits;
        entity.CardHolderName = source.Card?.Cardholder?.Name;
        entity.AuthorizationCode = source.AuthorizationCode;
        entity.FeeAmount = source.FeeDetails?.FirstOrDefault()?.Amount;
        entity.NetReceivedAmount = source.TransactionDetails?.NetReceivedAmount;
        entity.Captured = source.Captured;
        entity.RefundedAmount = GetTotalRefundedAmount(source);
        entity.DateCreatedMp = source.DateCreated;
        entity.DateApproved = source.DateApproved;
        entity.DateLastUpdated = source.DateLastUpdated;
        entity.TransactionDetails = JsonSerializer.Serialize(source.TransactionDetails);
    }
}
