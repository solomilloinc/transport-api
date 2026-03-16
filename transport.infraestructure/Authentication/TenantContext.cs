using Transport.Business.Authentication;

namespace Transport.Infraestructure.Authentication;

public class TenantContext : ITenantContext
{
    public int TenantId { get; set; }
    public string? TenantCode { get; set; }
}
