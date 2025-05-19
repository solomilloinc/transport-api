using Transport.Business.Authentication;

namespace Transport.Infraestructure.Authentication;

public class UserContext : IUserContext
{
    public int UserId { get; set; }
    public string? Email { get; set; }
}
