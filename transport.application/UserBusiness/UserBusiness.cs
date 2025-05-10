using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.User;
using Transport.Domain.Users;
using Transport.Domain.Users.Abstraction;
using Transport.Business.Authorization;
using Transport.Business.Data;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Authentication;

namespace Transport.Business.UserBusiness;

public class UserBusiness : IUserBusiness
{
    private readonly IJwtService jwtService;
    private readonly IApplicationDbContext dbContext;
    private readonly IPasswordHasher passwordHasher;
    private readonly ITokenProvider tokenProvider;

    public UserBusiness(IJwtService jwtService, IApplicationDbContext dbContext, IPasswordHasher passwordHasher, ITokenProvider tokenProvider)
    {
        this.jwtService = jwtService;
        this.dbContext = dbContext;
        this.passwordHasher = passwordHasher;
        this.tokenProvider = tokenProvider;
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

        var token = jwtService.BuildToken(tokens);

        var refreshToken = new RefreshToken
        {
            Token = tokenProvider.GenerateRefreshToken(),
            UserId = user.UserId,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = login.IpAddress,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await dbContext.RefreshTokens.AddAsync(refreshToken);
        await dbContext.SaveChangesWithOutboxAsync();

        return new LoginResponseDto(token, refreshToken.Token);
    }

    public async Task<Result<RefreshTokenResponseDto>> RenewTokenAsync(string refreshToken, string ipAddress)
    {
        var storedRefreshToken = await tokenProvider.GetRefreshTokenAsync(refreshToken);

        if (storedRefreshToken == null)
            return Result.Failure<RefreshTokenResponseDto>(RefreshTokenError.RefreshNotFound);

        var user = await dbContext.Users.FindAsync(storedRefreshToken.UserId);
        var claims = ClaimBuilder.Create()
            .SetEmail(user.Email)
            .SetRole(((RoleEnum)user.Role.RoleId).ToString())
            .SetId(user.UserId.ToString())
            .Build();

        var newAccessToken = jwtService.BuildToken(claims);

        var newRefreshToken = tokenProvider.GenerateRefreshToken();
        await tokenProvider.SaveRefreshTokenAsync(newRefreshToken, user.UserId, ipAddress);

        await tokenProvider.RevokeRefreshTokenAsync(refreshToken, ipAddress);

        return new RefreshTokenResponseDto(newAccessToken, newRefreshToken);
    }

}
