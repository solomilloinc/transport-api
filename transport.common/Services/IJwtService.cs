using System.Security.Claims;

namespace transport.common.Services;

public interface IJwtService
{
    string BuildToken(IEnumerable<Claim> claims);
}
