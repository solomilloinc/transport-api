using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Transport.SharedKernel.Configuration;

namespace Transport.Infraestructure.Authorization
{
    public interface IAuthorizationService
    {
        bool CheckAuthorization(string bearerToken, out ClaimsPrincipal claims, string[]? roles = null);
        string GetSpecificClaim(string claimType);
    }

    internal class AuthorizationService : IAuthorizationService
    {
        private readonly IJwtOption JwtOption;

        public AuthorizationService(IJwtOption JwtOption)
        {
            this.JwtOption = JwtOption;
        }

        private ClaimsPrincipal? Claims { get; set; }

        public bool CheckAuthorization(string bearerToken, out ClaimsPrincipal claims, string[]? roles = null)
        {
            claims = null;
            var token = bearerToken.Replace("Bearer ", string.Empty);
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(JwtOption.Secret));
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = JwtOption.Issuer,
                ValidateAudience = true,
                ValidAudience = JwtOption.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RequireExpirationTime = true,
            };

            try
            {
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
                claims = principal;

                if (roles != null)
                {
                    var role = principal.FindFirst(ClaimTypes.Role)?.Value;
                    if (role == null) return false;
                    return roles.Contains(role, StringComparer.InvariantCultureIgnoreCase);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetSpecificClaim(string claimType)
        {
            return Claims?.FindFirst(claimType)?.Value!;
        }
    }
}
