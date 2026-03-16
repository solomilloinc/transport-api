using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.User;
using Transport.Domain.Users;
using Transport.Domain.Users.Abstraction;
using Transport.Business.Authorization;
using Transport.Business.Data;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Authentication;
using Transport.SharedKernel.Helpers;

namespace Transport.Business.UserBusiness;

public class UserBusiness : IUserBusiness
{
    private readonly IJwtService jwtService;
    private readonly IApplicationDbContext dbContext;
    private readonly IPasswordHasher passwordHasher;
    private readonly ITokenProvider tokenProvider;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITenantContext tenantContext;

    public UserBusiness(IJwtService jwtService, IApplicationDbContext dbContext, IPasswordHasher passwordHasher, ITokenProvider tokenProvider, IUnitOfWork unitOfWork, ITenantContext tenantContext)
    {
        this.jwtService = jwtService;
        this.dbContext = dbContext;
        this.passwordHasher = passwordHasher;
        this.tokenProvider = tokenProvider;
        this.unitOfWork = unitOfWork;
        this.tenantContext = tenantContext;
    }

    public async Task<Result<LoginResponseDto>> Login(LoginDto login)
    {
        var tenantId = tenantContext.TenantId;

        var user = await dbContext.Users
            .Include(u => u.Role)
            .Where(u => u.Email == login.Email && u.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (user is null || !passwordHasher.Verify(login.Password, user.Password))
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        var tokens = ClaimBuilder.Create()
            .SetEmail(user.Email)
            .SetRole(((RoleEnum)user.Role.RoleId).ToString())
            .SetId(user.UserId.ToString())
            .SetTenantId(user.TenantId)
            .Build();

        var accessToken = jwtService.BuildToken(tokens);

        var refreshToken = tokenProvider.GenerateRefreshToken();

        await tokenProvider.SaveRefreshTokenAsync(refreshToken, user.UserId, login.IpAddress);

        return new LoginResponseDto(accessToken, refreshToken);
    }


    public async Task<Result<RefreshTokenResponseDto>> RenewTokenAsync(string refreshToken, string ipAddress)
    {
        // Buscar el token sin filtrar por estado para detectar reutilización
        var storedRefreshToken = await tokenProvider.GetRefreshTokenByHashAsync(refreshToken);

        if (storedRefreshToken == null)
            return Result.Failure<RefreshTokenResponseDto>(RefreshTokenError.RefreshNotFound);

        // Detectar reutilización de token (posible robo)
        if (storedRefreshToken.RevokedAt != null)
        {
            // Token ya fue usado/revocado - revocar toda la familia por seguridad
            await tokenProvider.RevokeAllUserTokensAsync(storedRefreshToken.UserId, ipAddress);
            return Result.Failure<RefreshTokenResponseDto>(RefreshTokenError.TokenReused);
        }

        // Verificar expiración
        if (storedRefreshToken.IsExpired)
            return Result.Failure<RefreshTokenResponseDto>(RefreshTokenError.TokenExpired);

        var user = await dbContext.Users.Where(x => x.UserId == storedRefreshToken.UserId).FirstOrDefaultAsync();
        var claims = ClaimBuilder.Create()
            .SetEmail(user!.Email)
            .SetRole(((RoleEnum)user.RoleId).ToString())
            .SetId(user.UserId.ToString())
            .SetTenantId(user.TenantId)
            .Build();

        var newAccessToken = jwtService.BuildToken(claims);

        var newRefreshToken = tokenProvider.GenerateRefreshToken();
        var hashedNewToken = TokenHasher.HashToken(newRefreshToken);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await tokenProvider.SaveRefreshTokenAsync(newRefreshToken, user.UserId, ipAddress);

            await tokenProvider.RevokeRefreshTokenAsync(refreshToken, ipAddress, replacedByToken: hashedNewToken);
        });

        return new RefreshTokenResponseDto(newAccessToken, newRefreshToken);
    }

    public async Task LogoutAsync(string refreshToken, string ipAddress)
    {
        await tokenProvider.RevokeRefreshTokenAsync(refreshToken, ipAddress);
    }

    public async Task RevokeAllSessionsAsync(int userId, string ipAddress)
    {
        await tokenProvider.RevokeAllUserTokensAsync(userId, ipAddress);
    }

}
