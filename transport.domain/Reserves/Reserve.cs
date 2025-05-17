using Transport.Domain.Customers;
using Transport.Domain.Drivers;
using Transport.Domain.Services;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;

namespace Transport.Domain.Reserves;
public class Reserve: Entity, IAuditable
{
    public int ReserveId { get; set; }
    public DateTime ReserveDate { get; set; }
    public ReserveStatusEnum Status { get; set; }
    public int VehicleId { get; set; }
    public int? DriverId { get; set; }
    public int ServiceId { get; set;}

    public Vehicle Vehicle { get; set; } = null!;
    public Driver? Driver { get; set; }
    public Service Service { get; set; } = null!;
    public ICollection<CustomerReserve> CustomerReserves { get; set; } = new List<CustomerReserve>();

    public string CreatedBy { get; set; } = null!;
    public string UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
}