using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.User;
using Transport.Domain.Users;
using Transport.Domain.Users.Abstraction;
using Transport.Business.Authorization;
using Transport.Business.Data;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Authentication;

namespace Transport.Business.UserBusiness;

public class LoginBusiness : ILoginBusiness
{
    private readonly IJwtService jwtService;
    private readonly IApplicationDbContext dbContext;
    private readonly IPasswordHasher passwordHasher;

    public LoginBusiness(IJwtService jwtService, IApplicationDbContext dbContext, IPasswordHasher passwordHasher)
    {
        this.jwtService = jwtService;
        this.dbContext = dbContext;
        this.passwordHasher = passwordHasher;
    }

    public async Task<Result<LoginResponseDto>> Login(LoginDto login)
    {
        var hasPass = passwordHasher.Hash(login.Password);

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

        return new LoginResponseDto(token);
    }
}
