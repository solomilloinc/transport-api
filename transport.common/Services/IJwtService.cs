using System.Security.Claims;

namespace Transport.SharedKernel.Services;

public interface IJwtService
{
    string BuildToken(IEnumerable<Claim> claims);
}
