using Transport.Domain.Directions;
using Transport.Domain.Reserves;
using Transport.SharedKernel;

namespace Transport.Domain.Customers;
public class CustomerReserve: IAuditable
{
    public int CustomerReserveId { get; set; }
    public int? UserId { get; set; }
    public int CustomerId { get; set; }
    public int ReserveId { get; set; }
    public bool IsPayment { get; set; }
    public CustomerReserveStatusEnum Status { get; set; }
    public decimal Price { get; set; }
    public int? PickupLocationId { get; set; }
    public int? DropoffLocationId { get; set; }
    public bool HasTraveled { get; set; }
    public int? ReferencePaymentId { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Customer Customer { get; set; } = null!;
    public Reserve Reserve { get; set; } = null!;
    public Direction PickupLocation { get; set; } = null!;
    public Direction DropoffLocation { get; set; } = null!;

    public string ServiceName { get; set; } = null!;
    public string OriginCityName { get; set; } = null!;
    public string DestinationCityName { get; set; } = null!;
    public string VehicleInternalNumber { get; set; } = null!;
    public string? DriverName { get; set; }
    public string? PickupAddress { get; set; }
    public string? DropoffAddress { get; set; }
    public string CustomerFullName { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public string? Phone1 { get; set; }
    public string? Phone2 { get; set; }

}
