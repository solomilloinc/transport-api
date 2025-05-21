using MercadoPago.Client.Payment;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Transport.Business.Data;
using Transport.Domain.Payments;
using Transport.Domain.Payments.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Payment;

namespace Transport.Business.PaymentBusiness;

public class PaymentBusiness : IPaymentBusiness
{
    private readonly IApplicationDbContext _context;

    public PaymentBusiness(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> CreatePayment(PaymentCreateRequestDto paymentData)
    {
        var paymentRequest = new PaymentCreateRequest
        {
            TransactionAmount = paymentData.TransactionAmount,
            Token = paymentData.Token,
            Description = paymentData.Description,
            Installments = paymentData.Installments,
            PaymentMethodId = paymentData.PaymentMethodId,
            Payer = new PaymentPayerRequest
            {
                Email = paymentData.Payer.Email
            }
        };

        var client = new PaymentClient();
        var result = await client.CreateAsync(paymentRequest);

        var statusEnum = result.Status.ToPaymentStatus();
        var statusDetailEnum = result.StatusDetail.ToPaymentStatusDetail();

        _context.Payments.Add(new Payment()
        {
            Amount = paymentData.TransactionAmount,
            Email = paymentData.Payer.Email,
            PaymentMpId = result.Id,
            RawJson = JsonSerializer.Serialize(result),
            Status = statusEnum.ToString()
        });

        await _context.SaveChangesWithOutboxAsync();

        return true;
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

        existing.Status = result.Status.ToPaymentStatus().ToString();
        existing.DateApproved = result.DateApproved;
        existing.RawJson = JsonSerializer.Serialize(result);

        _context.Payments.Update(existing);
        await _context.SaveChangesWithOutboxAsync();

        return true;
    }

}
