using Transport.Domain.Cities;
using Transport.Domain.Directions;
using Transport.Domain.Reserves;
using Transport.SharedKernel;

namespace Transport.Domain.Trips;

public class TripPrice : IAuditable, ITenantScoped
{
    public int TripPriceId { get; set; }
    public int TenantId { get; set; }
    public int TripId { get; set; }
    public int CityId { get; set; }
    public int? DirectionId { get; set; }
    public ReserveTypeIdEnum ReserveTypeId { get; set; }
    public decimal Price { get; set; }
    public int Order { get; set; }
    public EntityStatusEnum Status { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    // Navegación
    public Trip Trip { get; set; } = null!;
    public City City { get; set; } = null!;
    public Direction? Direction { get; set; }
}
