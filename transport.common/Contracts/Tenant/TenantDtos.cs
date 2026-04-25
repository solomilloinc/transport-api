namespace Transport.SharedKernel.Contracts.Tenant;

public record TenantCreateRequestDto(string Code, string Name, string? Domain);

public record TenantUpdateRequestDto(string Name, string? Domain);

public record TenantResponseDto(int TenantId, string Code, string Name, string? Domain, string Status);

public record TenantPaymentConfigUpdateRequestDto(string AccessToken, string PublicKey, string? WebhookSecret);

public record TenantConfigUpdateRequestDto(
    // Identity
    string? CompanyName,
    string? CompanyNameShort,
    string? CompanyNameLegal,
    string? LogoUrl,
    string? FaviconUrl,
    string? Tagline,
    // Contact
    string? ContactAddress,
    string? ContactPhone,
    string? ContactEmail,
    string? BookingsEmail,
    // Legal
    string? TermsText,
    string? CancellationPolicy,
    // Style (raw JSON: theme, typography, images, landing, seo, contact.schedule)
    string? StyleConfigJson
);

public record TenantReserveConfigResponseDto(int TenantId, bool RoundTripSameDayOnly);

public record TenantReserveConfigUpdateRequestDto(bool RoundTripSameDayOnly);
