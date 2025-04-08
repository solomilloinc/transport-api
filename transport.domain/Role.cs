namespace transport.domain;

public class Role
{
    public int RoleId { get; set; }
    public string Name { get; set; } = null!;

    public ICollection<User> Users { get; set; } = new List<User>();
}

public enum RoleEnum
{
    Admin = 1,
    User = 2,
}
