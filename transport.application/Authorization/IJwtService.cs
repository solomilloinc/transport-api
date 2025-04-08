using System.Security.Claims;

namespace transport.application.Authorization;

public interface IJwtService
{
    string BuildToken(IEnumerable<Claim> claims);
}
