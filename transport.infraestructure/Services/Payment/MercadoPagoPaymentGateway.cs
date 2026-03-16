using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Business.Services.Payment;
using Transport.Domain.Tenants;
using Transport.SharedKernel.Configuration;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Infraestructure.Services.Payment;

public class MercadoPagoPaymentGateway : IMercadoPagoPaymentGateway
{
    private readonly IMpIntegrationOption _mpIntegrationOption;
    private readonly ITenantContext _tenantContext;
    private readonly IApplicationDbContext _dbContext;

    public MercadoPagoPaymentGateway(
        IMpIntegrationOption mpIntegrationOption,
        ITenantContext tenantContext,
        IApplicationDbContext dbContext)
    {
        _mpIntegrationOption = mpIntegrationOption;
        _tenantContext = tenantContext;
        _dbContext = dbContext;
    }

    public async Task<MercadoPago.Resource.Payment.Payment> CreatePaymentAsync(PaymentCreateRequest request)
    {
        MercadoPagoConfig.AccessToken = await ResolveAccessTokenAsync();

        var client = new PaymentClient();
        return await client.CreateAsync(request);
    }

    public async Task<string> CreatePreferenceAsync(string externalReference, decimal totalAmount, List<PassengerReserveExternalCreateRequestDto> passengers)
    {
        MercadoPagoConfig.AccessToken = await ResolveAccessTokenAsync();

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
        MercadoPagoConfig.AccessToken = await ResolveAccessTokenAsync();
        var client = new PaymentClient();
        return await client.GetAsync(long.Parse(paymentId));
    }

    private async Task<string> ResolveAccessTokenAsync()
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId > 0)
        {
            var tenantConfig = await _dbContext.TenantPaymentConfigs
                .FirstOrDefaultAsync(c => c.TenantId == tenantId);

            if (tenantConfig is not null)
                return tenantConfig.AccessToken;
        }

        // Fallback to global config
        return _mpIntegrationOption.AccessToken;
    }
}
