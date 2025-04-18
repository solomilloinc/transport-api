using Transport.Domain.Users;
using Transport.SharedKernel;

namespace Transport.Domain.Customers;

public class Customer
{
    public int CustomerId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public string Phone1 { get; set; } = null!;
    public string? Phone2 { get; set; }
    public EntityStatusEnum Status { get; set; }
    public User? User { get; set; }
    public ICollection<ServiceCustomer> Services { get; set; } = new List<ServiceCustomer>();
    public ICollection<CustomerReserve> CustomerReserves { get; set; } = new List<CustomerReserve>();
}
