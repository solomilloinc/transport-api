using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.Users;
using Transport.SharedKernel.Helpers;

namespace Transport.Infraestructure.Authentication;

internal sealed class TokenProvider(IConfiguration configuration, IApplicationDbContext dbContext) : ITokenProvider
{
    public string Create(User user)
    {
        string secretKey = configuration["Jwt:Secret"]!;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
            ]),
            Expires = DateTime.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:ExpirationInMinutes")),
            SigningCredentials = credentials,
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"]
        };

        var handler = new JsonWebTokenHandler();

        string token = handler.CreateToken(tokenDescriptor);

        return token;
    }

    public async Task SaveRefreshTokenAsync(string refreshToken, int userId, string ipAddress)
    {
        var hashedToken = TokenHasher.HashToken(refreshToken);

        var token = new RefreshToken
        {
            Token = hashedToken,
            UserId = userId,
            CreatedByIp = ipAddress,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.RefreshTokens.AddAsync(token);
        await dbContext.SaveChangesWithOutboxAsync();
    }

    public async Task<RefreshToken> GetRefreshTokenAsync(string token)
    {
        var hashedToken = TokenHasher.HashToken(token);

        return await dbContext.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.Token == hashedToken && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow);
    }

    public async Task RevokeRefreshTokenAsync(string token, string ipAddress)
    {
        var refreshToken = await GetRefreshTokenAsync(token);

        if (token == null) return;


        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.RevokedByIp = ipAddress;

        dbContext.RefreshTokens.Update(refreshToken);
        await dbContext.SaveChangesWithOutboxAsync();
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
