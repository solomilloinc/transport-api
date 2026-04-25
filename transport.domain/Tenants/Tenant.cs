using Transport.SharedKernel;

namespace Transport.Domain.Tenants;

public class Tenant : IAuditable
{
    public int TenantId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Domain { get; set; }
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public TenantConfig? Config { get; set; }
    public TenantPaymentConfig? PaymentConfig { get; set; }
    public TenantReserveConfig? ReserveConfig { get; set; }
}
