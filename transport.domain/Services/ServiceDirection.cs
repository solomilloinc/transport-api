using Transport.Domain.Directions;
using Transport.SharedKernel;

namespace Transport.Domain.Services;

/// <summary>
/// Whitelist of allowed directions for a service.
/// Used to filter available pickup/dropoff options for reserves created from this service.
/// </summary>
public class ServiceDirection : Entity, IAuditable, ITenantScoped
{
    public int ServiceDirectionId { get; set; }
    public int TenantId { get; set; }
    public int ServiceId { get; set; }
    public int DirectionId { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    // Navigation
    public Service Service { get; set; } = null!;
    public Direction Direction { get; set; } = null!;
}
