using transport.application.Authorization;
using transport.common;
using transport.domain;

namespace Transport.Business.UserBusiness;

public interface ILoginBusiness
{
    Task<Result<LoginResponseDto>> Login(LoginDto login);
}

public class LoginBusiness : ILoginBusiness
{
    private readonly IJwtService jwtService;

    public LoginBusiness(IJwtService jwtService)
    {
        this.jwtService = jwtService;
    }

    public async Task<Result<LoginResponseDto>> Login(LoginDto login)
    {
        User user = new User
        {
            UserId = 1,
            Email = "agustinyuse@gmail.com",
            Password = "123456",
            RoleId = (int)RoleEnum.Admin
        };

        var tokens = ClaimBuilder.Create()
            .SetEmail(user.Email)
            .SetRole("Admin")
            .SetId(user.UserId.ToString())
            .Build();

        var token = jwtService.BuildToken(tokens);

        return new LoginResponseDto(token);
    }
}
