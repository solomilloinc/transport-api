using Transport.Domain.FrequentSubscriptions;
using Transport.Domain.Reserves;
using Transport.Domain.Trips;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;

namespace Transport.Domain.Services;
public class Service : Entity, IAuditable, ITenantScoped
{
    public int ServiceId { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = null!;
    public int TripId { get; set; }
    public int VehicleId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan DepartureHour { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public bool IsHoliday { get; set; }
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Trip Trip { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;

    public ICollection<Reserve> Reserves { get; set; } = new List<Reserve>();
    public ICollection<ServiceDirection> AllowedDirections { get; set; } = new List<ServiceDirection>();

    // Suscripciones donde este Service es el outbound (la Ida del cliente).
    public ICollection<FrequentSubscription> OutboundSubscriptions { get; set; } = new List<FrequentSubscription>();

    // Suscripciones donde este Service es el inbound (la Vuelta de un IdaVuelta).
    public ICollection<FrequentSubscription> InboundSubscriptions { get; set; } = new List<FrequentSubscription>();
}
