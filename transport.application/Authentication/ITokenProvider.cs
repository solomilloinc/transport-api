using transport.domain;

namespace transport.application.Authentication;

public interface ITokenProvider
{
    string Create(User user);
}
