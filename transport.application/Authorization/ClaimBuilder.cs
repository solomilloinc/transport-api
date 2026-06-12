using System.Security.Claims;

namespace Transport.Business.Authorization;

public class ClaimBuilder
{
    private ICollection<Claim> claims = new HashSet<Claim>();

    public static ClaimBuilder Create() => new();

    public ClaimBuilder SetId(string id)
    {
        claims.Add(new Claim(ClaimTypes.NameIdentifier, id));
        return this;
    }

    public ClaimBuilder SetName(string name)
    {
        claims.Add(new Claim(ClaimTypes.Name, name));
        return this;
    }

    public ClaimBuilder SetRole(string role)
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
        return this;
    }

    public ClaimBuilder SetEmail(string email)
    {
        claims.Add(new Claim(ClaimTypes.Email, email));
        return this;
    }

    public ClaimBuilder SetTenantId(int tenantId)
    {
        claims.Add(new Claim("tenant_id", tenantId.ToString()));
        return this;
    }

    public ClaimBuilder SetCustomerId(int? customerId)
    {
        if (customerId.HasValue)
        {
            claims.Add(new Claim("customer_id", customerId.Value.ToString()));
        }

        return this;
    }

    public ClaimBuilder SetNeedsProfileCompletion(bool needsProfileCompletion)
    {
        claims.Add(new Claim("needs_profile_completion", needsProfileCompletion.ToString().ToLowerInvariant()));
        return this;
    }

    public ICollection<Claim> Build() => claims;
}
