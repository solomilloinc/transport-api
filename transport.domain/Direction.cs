using Transport.Domain.Cities;
using Transport.Domain.Customers;
using Transport.SharedKernel;

namespace Transport.Domain;

public class Direction: IAuditable
{
    public int DirectionId { get; set; }
    public string Name { get; set; } = null!;
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public int CityId { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public City City { get; set; } = null!;
    public ICollection<CustomerReserve> PickupCustomerReserves { get; set; } = new List<CustomerReserve>();
    public ICollection<CustomerReserve> DropoffCustomerReserves { get; set; } = new List<CustomerReserve>();
}
