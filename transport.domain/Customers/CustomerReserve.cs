using Transport.Domain.Reserves;

namespace Transport.Domain.Customers;
public class CustomerReserve
{
    public int CustomerReserveId { get; set; }
    public int CustomerId { get; set; }
    public int ReserveId { get; set; }
    public bool IsPayment { get; set; }
    public string StatusPayment { get; set; } = null!;
    public decimal Price { get; set; }
    public int PickupLocationId { get; set; }
    public int DropoffLocationId { get; set; }
    public bool HasTraveled { get; set; }

    public Customer Customer { get; set; } = null!;
    public Reserve Reserve { get; set; } = null!;
    public Direction PickupLocation { get; set; } = null!;
    public Direction DropoffLocation { get; set; } = null!;
}
