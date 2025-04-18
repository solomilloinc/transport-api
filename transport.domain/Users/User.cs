﻿using Transport.Domain.Customers;

namespace Transport.Domain.Users;

public class User
{
    public int UserId { get; set; }
    public int? CustomerId { get; set; }
    public int RoleId { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public Role Role { get; set; } = null!;
    public Customer? Customer { get; set; }
}
