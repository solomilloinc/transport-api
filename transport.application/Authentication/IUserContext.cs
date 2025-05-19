namespace Transport.Business.Authentication;

public interface IUserContext
{
    int UserId { get; }
    string? Email { get; }
}

