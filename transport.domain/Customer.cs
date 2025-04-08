namespace transport.domain;

public class Customer
{
    public int CustomerId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public string Phone1 { get; set; } = null!;
    public string? Phone2 { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<ServiceCustomer> Services { get; set; } = new List<ServiceCustomer>();
    public ICollection<CustomerReserve> CustomerReserves { get; set; } = new List<CustomerReserve>();
}
