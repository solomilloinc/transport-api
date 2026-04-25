using Transport.SharedKernel;

namespace Transport.Domain.Tenants;

/// <summary>
/// Per-tenant business rules for reservations and pricing.
/// Kept separate from <see cref="TenantConfig"/> (identity/branding) for SRP.
/// </summary>
public class TenantReserveConfig : IAuditable
{
    public int TenantReserveConfigId { get; set; }
    public int TenantId { get; set; }

    /// <summary>
    /// When true, the IdaVuelta (round-trip) combo price only applies if the
    /// outbound and return reservations are on the same calendar date.
    /// Otherwise each leg is charged at the single Ida (one-way) price.
    /// </summary>
    public bool RoundTripSameDayOnly { get; set; } = true;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
