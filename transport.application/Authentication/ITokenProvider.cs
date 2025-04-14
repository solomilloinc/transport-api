using Transport.Domain.Users;

namespace Transport.Business.Authentication;

public interface ITokenProvider
{
    string Create(User user);
}
