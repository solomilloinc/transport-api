using Transport.Domain.Cities;
using Transport.SharedKernel;

namespace Transport.Domain.Trips;

public class Trip : IAuditable
{
    public int TripId { get; set; }
    public string Description { get; set; } = null!;
    public int OriginCityId { get; set; }
    public int DestinationCityId { get; set; }
    public EntityStatusEnum Status { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    // Navegación
    public City OriginCity { get; set; } = null!;
    public City DestinationCity { get; set; } = null!;
    public ICollection<TripPrice> Prices { get; set; } = new List<TripPrice>();
}
