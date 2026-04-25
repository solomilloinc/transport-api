namespace Transport.Domain.Tenants.Abstraction;

/// <summary>
/// Provides access to the current tenant's reserve configuration.
/// Implementations are expected to be scoped per request and to cache
/// the configuration after the first read to avoid repeated database hits
/// during a single business operation.
/// </summary>
public interface ITenantReserveConfigProvider
{
    /// <summary>
    /// Returns the reserve configuration for the current tenant.
    /// If no row exists yet for the tenant, returns a default configuration
    /// (RoundTripSameDayOnly = true) without persisting it.
    /// </summary>
    Task<TenantReserveConfig> GetCurrentAsync(CancellationToken cancellationToken = default);
}
