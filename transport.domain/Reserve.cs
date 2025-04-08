using transport.domain.Drivers;

namespace transport.domain;
public class Reserve
{
    public int ReserveId { get; set; }
    public DateTime ReserveDate { get; set; }
    public bool Status { get; set; }
    public int VehicleId { get; set; }
    public int? DriverId { get; set; }
    public int ServiceId { get; set; }

    public Driver? Driver { get; set; }
    public Service Service { get; set; } = null!;
    public ICollection<CustomerReserve> CustomerReserves { get; set; } = new List<CustomerReserve>();
}
