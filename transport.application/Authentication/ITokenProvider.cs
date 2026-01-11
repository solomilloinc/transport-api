using System.Security.Cryptography;
using Transport.Domain.Users;

namespace Transport.Business.Authentication;

public interface ITokenProvider
{
    string Create(User user);
    Task<RefreshToken?> GetRefreshTokenAsync(string token);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string token);
    Task SaveRefreshTokenAsync(string refreshToken, int userId, string ipAddress);
    Task RevokeRefreshTokenAsync(string token, string ipAddress, string? replacedByToken = null);
    Task RevokeAllUserTokensAsync(int userId, string ipAddress);
    Task<int> CleanupExpiredTokensAsync(int daysOld = 30);
    string GenerateRefreshToken();
}
