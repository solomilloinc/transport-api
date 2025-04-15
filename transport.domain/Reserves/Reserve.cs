using Transport.Domain.Customers;
using Transport.Domain.Drivers;
using Transport.Domain.Vehicles;

namespace Transport.Domain.Reserves;
public class Reserve
{
    public int ReserveId { get; set; }
    public DateTime ReserveDate { get; set; }
    public ReserveStatusEnum Status { get; set; }
    public int VehicleId { get; set; }
    public int? DriverId { get; set; }
    public int ServiceId { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
    public Driver? Driver { get; set; }
    public Service Service { get; set; } = null!;
    public ICollection<CustomerReserve> CustomerReserves { get; set; } = new List<CustomerReserve>();
}
