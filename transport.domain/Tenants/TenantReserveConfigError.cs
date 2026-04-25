using Transport.SharedKernel;

namespace Transport.Domain.Tenants;

public static class TenantReserveConfigError
{
    public static readonly Error NotFound = Error.NotFound(
        "TenantReserveConfig.NotFound",
        "Reserve configuration not found for this tenant");
}
