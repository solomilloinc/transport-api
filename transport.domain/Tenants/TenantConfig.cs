using Transport.SharedKernel;

namespace Transport.Domain.Tenants;

public class TenantConfig : IAuditable
{
    public int TenantConfigId { get; set; }
    public int TenantId { get; set; }
    public string ConfigJson { get; set; } = "{}";

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
