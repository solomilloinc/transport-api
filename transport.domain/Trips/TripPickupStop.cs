using Transport.Domain.Directions;
using Transport.SharedKernel;

namespace Transport.Domain.Trips;

public class TripPickupStop : IAuditable
{
    public int TripPickupStopId { get; set; }
    public int TripId { get; set; }
    public int DirectionId { get; set; }
    public int Order { get; set; }
    public TimeSpan PickupTimeOffset { get; set; }

    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;
    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Trip Trip { get; set; } = null!;
    public Direction Direction { get; set; } = null!;
}
