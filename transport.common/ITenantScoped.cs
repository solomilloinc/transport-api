namespace Transport.SharedKernel;

public interface ITenantScoped
{
    int TenantId { get; set; }
}
