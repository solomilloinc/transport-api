using System.Security.Claims;

namespace Transport.Business.Authorization;

public interface IJwtService
{
    string BuildToken(IEnumerable<Claim> claims);
}
