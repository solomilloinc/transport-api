using Transport.Domain.Cities;
using Transport.Domain.Customers;
using Transport.Domain.Passengers;
using Transport.SharedKernel;

namespace Transport.Domain.Directions;

public class Direction: IAuditable
{
    public int DirectionId { get; set; }
    public string Name { get; set; } = null!;
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public int CityId { get; set; }
    public EntityStatusEnum Status { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public City City { get; set; } = null!;
    public ICollection<Passenger> PickupCustomerReserves { get; set; } = new List<Passenger>();
    public ICollection<Passenger> DropoffCustomerReserves { get; set; } = new List<Passenger>();
}
