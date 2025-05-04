using Transport.Domain.Cities;
using Transport.Domain.Customers;

namespace Transport.Domain;

public class Direction
{
    public int DirectionId { get; set; }
    public string Name { get; set; } = null!;
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public int CityId { get; set; }

    public City City { get; set; } = null!;
    public ICollection<CustomerReserve> PickupCustomerReserves { get; set; } = new List<CustomerReserve>();
    public ICollection<CustomerReserve> DropoffCustomerReserves { get; set; } = new List<CustomerReserve>();
}
