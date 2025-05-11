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

    public UserBusiness(IJwtService jwtService, IApplicationDbContext dbContext, IPasswordHasher passwordHasher, ITokenProvider tokenProvider, IUnitOfWork unitOfWork)
    {
        this.jwtService = jwtService;
        this.dbContext = dbContext;
        this.passwordHasher = passwordHasher;
        this.tokenProvider = tokenProvider;
        this.unitOfWork = unitOfWork;
    }

    public async Task<Result<LoginResponseDto>> Login(LoginDto login)
    {
        var user = await dbContext.Users
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.Email == login.Email);

        if (user is null || !passwordHasher.Verify(login.Password, user.Password))
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        var tokens = ClaimBuilder.Create()
            .SetEmail(user.Email)
            .SetRole(((RoleEnum)user.Role.RoleId).ToString())
            .SetId(user.UserId.ToString())
            .Build();

        var accessToken = jwtService.BuildToken(tokens);

        var refreshToken = tokenProvider.GenerateRefreshToken();

        await tokenProvider.SaveRefreshTokenAsync(refreshToken, user.UserId, login.IpAddress);

        return new LoginResponseDto(accessToken, refreshToken);
    }


    public async Task<Result<RefreshTokenResponseDto>> RenewTokenAsync(string refreshToken, string ipAddress)
    {
        var storedRefreshToken = await tokenProvider.GetRefreshTokenAsync(refreshToken);

        if (storedRefreshToken == null)
            return Result.Failure<RefreshTokenResponseDto>(RefreshTokenError.RefreshNotFound);

        var user = await dbContext.Users.FindAsync(storedRefreshToken.UserId);
        var claims = ClaimBuilder.Create()
            .SetEmail(user.Email)
            .SetRole(((RoleEnum)user.RoleId).ToString())
            .SetId(user.UserId.ToString())
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

}
