using Transport.SharedKernel;

namespace Transport.Domain.Tenants;

public class TenantConfig : IAuditable
{
    public int TenantConfigId { get; set; }
    public int TenantId { get; set; }

    // Identity
    public string? CompanyName { get; set; }
    public string? CompanyNameShort { get; set; }
    public string? CompanyNameLegal { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? Tagline { get; set; }

    // Contact
    public string? ContactAddress { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? BookingsEmail { get; set; }

    // Legal
    public string? TermsText { get; set; }
    public string? CancellationPolicy { get; set; }

    // Visual/style config (theme, typography, images, landing, seo, contact.schedule)
    public string StyleConfigJson { get; set; } = "{}";

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
