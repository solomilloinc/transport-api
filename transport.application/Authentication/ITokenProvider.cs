using System.Security.Cryptography;
using Transport.Domain.Users;

namespace Transport.Business.Authentication;

public interface ITokenProvider
{
    string Create(User user);
    Task<RefreshToken> GetRefreshTokenAsync(string token);
    Task SaveRefreshTokenAsync(string refreshToken, int userId, string ipAddress);
    Task RevokeRefreshTokenAsync(string token, string ipAddress, string? replacedByToken = null);
    string GenerateRefreshToken();
}
