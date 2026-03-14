using Transport.SharedKernel;

namespace Transport.Domain.Tenants;

public class TenantPaymentConfig : IAuditable
{
    public int TenantPaymentConfigId { get; set; }
    public int TenantId { get; set; }
    public string AccessToken { get; set; } = null!;
    public string PublicKey { get; set; } = null!;
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
