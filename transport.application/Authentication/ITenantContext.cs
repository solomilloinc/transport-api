namespace Transport.Business.Authentication;

public interface ITenantContext
{
    int TenantId { get; }
    string? TenantCode { get; }
}
