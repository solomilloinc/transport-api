using Transport.Domain.Reserves;

namespace Transport.Domain;

public class Service
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = null!;
    public byte DayStart { get; set; }
    public byte DayEnd { get; set; }
    public int OriginId { get; set; }
    public int DestinationId { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public TimeSpan DepartureHour { get; set; }
    public bool IsHoliday { get; set; }
    public int VehicleId { get; set; }
    public bool Status { get; set; }

    public City Origin { get; set; } = null!;
    public City Destination { get; set; } = null!;
    public ICollection<ServiceCustomer> Customers { get; set; } = new List<ServiceCustomer>();
    public ICollection<Reserve> Reserves { get; set; } = new List<Reserve>();
    public ICollection<ReservePrice> ReservePrices { get; set; } = new List<ReservePrice>();
}
