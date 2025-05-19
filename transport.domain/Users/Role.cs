using Transport.SharedKernel;

namespace Transport.Domain.Users;

public class Role: IAuditable
{
    public int RoleId { get; set; }
    public string Name { get; set; } = null!;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}

public enum RoleEnum
{
    Admin = 1,
    User = 2,
}
