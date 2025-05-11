using Transport.SharedKernel.Contracts.User;
using Transport.SharedKernel;

namespace Transport.Domain.Users.Abstraction;

public interface IUserBusiness
{
    Task<Result<LoginResponseDto>> Login(LoginDto login);
    Task<Result<RefreshTokenResponseDto>> RenewTokenAsync(string token, string ipAddress);
    Task LogoutAsync(string refreshToken, string ipAddress);
}
