using Transport.Domain.Customers;
using Transport.SharedKernel;

namespace Transport.Domain.Services;

public class ServiceCustomer: IAuditable
{
    public int ServiceCustomerId { get; set; }
    public int ServiceId { get; set; }
    public int CustomerId { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Service Service { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
