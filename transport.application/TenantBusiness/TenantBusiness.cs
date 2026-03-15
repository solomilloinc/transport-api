using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.Tenants;
using Transport.Domain.Tenants.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Tenant;

namespace Transport.Business.TenantBusiness;

public class TenantBusiness : ITenantBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public TenantBusiness(IApplicationDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<Result<TenantResponseDto>> Create(TenantCreateRequestDto request)
    {
        var existingByCode = await _context.Tenants
            .AnyAsync(t => t.Code == request.Code);

        if (existingByCode)
            return Result.Failure<TenantResponseDto>(TenantError.CodeAlreadyExists);

        if (!string.IsNullOrWhiteSpace(request.Domain))
        {
            var existingByDomain = await _context.Tenants
                .AnyAsync(t => t.Domain == request.Domain);

            if (existingByDomain)
                return Result.Failure<TenantResponseDto>(TenantError.DomainAlreadyExists);
        }

        var tenant = new Tenant
        {
            Code = request.Code.ToLowerInvariant(),
            Name = request.Name,
            Domain = request.Domain,
            Status = EntityStatusEnum.Active
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesWithOutboxAsync();

        return ToDto(tenant);
    }

    public async Task<Result<TenantResponseDto>> Update(int tenantId, TenantUpdateRequestDto request)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId);

        if (tenant is null)
            return Result.Failure<TenantResponseDto>(TenantError.NotFound);

        if (!string.IsNullOrWhiteSpace(request.Domain))
        {
            var existingByDomain = await _context.Tenants
                .AnyAsync(t => t.Domain == request.Domain && t.TenantId != tenantId);

            if (existingByDomain)
                return Result.Failure<TenantResponseDto>(TenantError.DomainAlreadyExists);
        }

        tenant.Name = request.Name;
        tenant.Domain = request.Domain;

        _context.Tenants.Update(tenant);
        await _context.SaveChangesWithOutboxAsync();

        return ToDto(tenant);
    }

    public async Task<Result<bool>> Delete(int tenantId)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId);

        if (tenant is null)
            return Result.Failure<bool>(TenantError.NotFound);

        tenant.Status = EntityStatusEnum.Deleted;

        _context.Tenants.Update(tenant);
        await _context.SaveChangesWithOutboxAsync();

        return true;
    }

    public async Task<Result<List<TenantResponseDto>>> GetAll()
    {
        var tenants = await _context.Tenants
            .Where(t => t.Status != EntityStatusEnum.Deleted)
            .Select(t => new TenantResponseDto(
                t.TenantId,
                t.Code,
                t.Name,
                t.Domain,
                t.Status.ToString()))
            .ToListAsync();

        return tenants;
    }

    public async Task<Result<bool>> UpdatePaymentConfig(int tenantId, TenantPaymentConfigUpdateRequestDto request)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId);

        if (tenant is null)
            return Result.Failure<bool>(TenantError.NotFound);

        var existing = await _context.TenantPaymentConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId);

        if (existing is not null)
        {
            existing.AccessToken = request.AccessToken;
            existing.PublicKey = request.PublicKey;
            existing.WebhookSecret = request.WebhookSecret;
            _context.TenantPaymentConfigs.Update(existing);
        }
        else
        {
            var config = new TenantPaymentConfig
            {
                TenantId = tenantId,
                AccessToken = request.AccessToken,
                PublicKey = request.PublicKey,
                WebhookSecret = request.WebhookSecret,
                Status = EntityStatusEnum.Active
            };
            _context.TenantPaymentConfigs.Add(config);
        }

        await _context.SaveChangesWithOutboxAsync();
        return true;
    }

    public async Task<Result<bool>> UpdateTenantConfig(int tenantId, TenantConfigUpdateRequestDto request)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId);

        if (tenant is null)
            return Result.Failure<bool>(TenantError.NotFound);

        var existing = await _context.TenantConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId);

        if (existing is not null)
        {
            existing.CompanyName = request.CompanyName;
            existing.CompanyNameShort = request.CompanyNameShort;
            existing.CompanyNameLegal = request.CompanyNameLegal;
            existing.LogoUrl = request.LogoUrl;
            existing.FaviconUrl = request.FaviconUrl;
            existing.Tagline = request.Tagline;
            existing.ContactAddress = request.ContactAddress;
            existing.ContactPhone = request.ContactPhone;
            existing.ContactEmail = request.ContactEmail;
            existing.BookingsEmail = request.BookingsEmail;
            existing.TermsText = request.TermsText;
            existing.CancellationPolicy = request.CancellationPolicy;
            if (request.StyleConfigJson is not null)
                existing.StyleConfigJson = request.StyleConfigJson;
            _context.TenantConfigs.Update(existing);
        }
        else
        {
            var config = new TenantConfig
            {
                TenantId = tenantId,
                CompanyName = request.CompanyName,
                CompanyNameShort = request.CompanyNameShort,
                CompanyNameLegal = request.CompanyNameLegal,
                LogoUrl = request.LogoUrl,
                FaviconUrl = request.FaviconUrl,
                Tagline = request.Tagline,
                ContactAddress = request.ContactAddress,
                ContactPhone = request.ContactPhone,
                ContactEmail = request.ContactEmail,
                BookingsEmail = request.BookingsEmail,
                TermsText = request.TermsText,
                CancellationPolicy = request.CancellationPolicy,
                StyleConfigJson = request.StyleConfigJson ?? "{}"
            };
            _context.TenantConfigs.Add(config);
        }

        await _context.SaveChangesWithOutboxAsync();
        return true;
    }

    public async Task<Result<string>> GetTenantConfig()
    {
        var tenantId = _tenantContext.TenantId;

        var config = await _context.TenantConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId);

        if (config is null)
            return Result.Failure<string>(TenantError.NotFound);

        return ComposeFrontendConfig(config);
    }

    /// <summary>
    /// Composes the full JSON the frontend expects by merging structured columns
    /// into the style JSON. Structured fields override any duplicates in StyleConfigJson.
    /// </summary>
    private static string ComposeFrontendConfig(TenantConfig config)
    {
        var styleJson = JObject.Parse(config.StyleConfigJson ?? "{}");

        // Identity — merge structured fields into identity section
        var identity = styleJson["identity"] as JObject ?? new JObject();
        SetIfNotNull(identity, "companyName", config.CompanyName);
        SetIfNotNull(identity, "companyNameShort", config.CompanyNameShort);
        SetIfNotNull(identity, "companyNameLegal", config.CompanyNameLegal);
        SetIfNotNull(identity, "logoUrl", config.LogoUrl);
        SetIfNotNull(identity, "faviconUrl", config.FaviconUrl);
        SetIfNotNull(identity, "tagline", config.Tagline);
        styleJson["identity"] = identity;

        // Contact — merge structured fields into contact section
        var contact = styleJson["contact"] as JObject ?? new JObject();
        SetIfNotNull(contact, "address", config.ContactAddress);
        SetIfNotNull(contact, "phone", config.ContactPhone);
        SetIfNotNull(contact, "email", config.ContactEmail);
        SetIfNotNull(contact, "bookingsEmail", config.BookingsEmail);
        styleJson["contact"] = contact;

        // Legal — merge structured fields into legal section
        var legal = styleJson["legal"] as JObject ?? new JObject();
        SetIfNotNull(legal, "termsText", config.TermsText);
        SetIfNotNull(legal, "cancellationPolicy", config.CancellationPolicy);
        styleJson["legal"] = legal;

        return styleJson.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static void SetIfNotNull(JObject obj, string key, string? value)
    {
        if (value is not null)
            obj[key] = value;
    }

    public async Task<Result<string>> ResolveTenantByHost(string host)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Domain == host && t.Status == EntityStatusEnum.Active);

        if (tenant is null)
            return Result.Failure<string>(TenantError.NotFound);

        var config = await _context.TenantConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.TenantId);

        var paymentConfig = await _context.TenantPaymentConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.TenantId);

        var result = new JObject
        {
            ["code"] = tenant.Code,
            ["publicKey"] = paymentConfig?.PublicKey
        };

        if (config is not null)
        {
            var configJson = JObject.Parse(ComposeFrontendConfig(config));
            result["config"] = configJson;
        }
        else
        {
            result["config"] = new JObject();
        }

        return result.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static TenantResponseDto ToDto(Tenant tenant) =>
        new(tenant.TenantId, tenant.Code, tenant.Name, tenant.Domain, tenant.Status.ToString());
}
