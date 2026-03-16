using Transport.Domain.Directions;
using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

/// <summary>
/// Whitelist of allowed directions for an individual reserve (not created from batch).
/// Used to filter available pickup/dropoff options for this specific reserve.
/// </summary>
public class ReserveDirection : Entity, IAuditable, ITenantScoped
{
    public int ReserveDirectionId { get; set; }
    public int TenantId { get; set; }
    public int ReserveId { get; set; }
    public int DirectionId { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    // Navigation
    public Reserve Reserve { get; set; } = null!;
    public Direction Direction { get; set; } = null!;
}
