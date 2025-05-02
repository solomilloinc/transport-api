using Transport.Domain.Customers;

namespace Transport.Domain.Services;

public class ServiceCustomer
{
    public int ServiceCustomerId { get; set; }
    public int ServiceId { get; set; }
    public int CustomerId { get; set; }

    public Service Service { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
