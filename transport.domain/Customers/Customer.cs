using Transport.Domain.Services;
using Transport.Domain.Users;
using Transport.SharedKernel;

namespace Transport.Domain.Customers;

public class Customer: Entity, IAuditable
{
    public int CustomerId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public string Phone1 { get; set; } = null!;
    public string? Phone2 { get; set; }
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;
    public User? User { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public ICollection<ServiceCustomer> Services { get; set; } = new List<ServiceCustomer>();
    public ICollection<CustomerReserve> CustomerReserves { get; set; } = new List<CustomerReserve>();
}
