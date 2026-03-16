using Transport.SharedKernel;

namespace Transport.Domain.Tenants;

public static class TenantError
{
    public static readonly Error NotFound = Error.NotFound("Tenant.NotFound", "Tenant not found");
    public static readonly Error InvalidCode = Error.Validation("Tenant.InvalidCode", "Invalid tenant code");
    public static readonly Error CodeAlreadyExists = Error.Conflict("Tenant.CodeAlreadyExists", "A tenant with this code already exists");
    public static readonly Error DomainAlreadyExists = Error.Conflict("Tenant.DomainAlreadyExists", "A tenant with this domain already exists");
    public static readonly Error MissingTenantHeader = Error.Validation("Tenant.MissingHeader", "X-Tenant-Code or X-Tenant-Domain header is required");
    public static readonly Error TenantMismatch = Error.Validation("Tenant.Mismatch", "Tenant header does not match authenticated tenant");
    public static readonly Error PaymentConfigNotFound = Error.NotFound("Tenant.PaymentConfigNotFound", "Payment configuration not found for this tenant");
}
