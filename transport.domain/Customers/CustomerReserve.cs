using Transport.Domain.Reserves;
using Transport.SharedKernel;

namespace Transport.Domain.Customers;
public class CustomerReserve: IAuditable
{
    public int CustomerReserveId { get; set; }
    public int? UserId { get; set; }
    public int CustomerId { get; set; }
    public int ReserveId { get; set; }
    public PaymentMethodEnum PaymentMethod { get; set; }
    public bool IsPayment { get; set; }
    public StatusPaymentEnum StatusPayment { get; set; }
    public decimal Price { get; set; }
    public int? PickupLocationId { get; set; }
    public int? DropoffLocationId { get; set; }
    public bool HasTraveled { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Customer Customer { get; set; } = null!;
    public Reserve Reserve { get; set; } = null!;
    public Direction PickupLocation { get; set; } = null!;
    public Direction DropoffLocation { get; set; } = null!;
}
