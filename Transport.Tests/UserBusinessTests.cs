using System.Data;
using System.Security.Claims;
using FluentAssertions;
using Moq;
using Transport.Business.Authentication;
using Transport.Business.Authorization;
using Transport.Business.Data;
using Transport.Business.UserBusiness;
using Transport.Domain.Users;
using Transport.Domain.Users.Abstraction;
using Transport.Infraestructure.Authorization;
using Transport.SharedKernel.Configuration;
using Transport.SharedKernel.Contracts.User;
using Xunit;

namespace Transport.Tests;

public class UserBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ITokenProvider> _tokenProviderMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly IUserBusiness _userBusiness;

    public UserBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _jwtServiceMock = new Mock<IJwtService>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenProviderMock = new Mock<ITokenProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _userBusiness = new UserBusiness(
            _jwtServiceMock.Object,
            _contextMock.Object,
            _passwordHasherMock.Object,
            _tokenProviderMock.Object,
            _unitOfWorkMock.Object
        );
    }

    [Fact]
    public async Task Login_ShouldFail_WhenInvalidCredentials()
    {
        var loginDto = new LoginDto("test@example.com", "password", "127.0.0.1");

        _contextMock.Setup(x => x.Users)
            .Returns(GetQueryableMockDbSet(new List<User>()).Object);
        _passwordHasherMock.Setup(p => p.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        Func<Task> action = async () => await _userBusiness.Login(loginDto);

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task Login_ShouldSucceed_WhenValidCredentials()
    {
        var loginDto = new LoginDto("test@example.com", "password", "127.0.0.1");
        var user = new User
        {
            UserId = 1,
            Email = "test@example.com",
            Password = "hashedPassword",
            Role = new Role { RoleId = 1, Name = "Admin" }
        };

        _contextMock.Setup(x => x.Users).Returns(GetQueryableMockDbSet(new List<User> { user }).Object);
        _passwordHasherMock.Setup(p => p.Verify("password", "hashedPassword")).Returns(true);
        _jwtServiceMock.Setup(j => j.BuildToken(It.IsAny<IEnumerable<Claim>>())).Returns("access-token");
        _tokenProviderMock.Setup(tp => tp.GenerateRefreshToken()).Returns("refresh-token");
        _tokenProviderMock.Setup(tp => tp.SaveRefreshTokenAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var result = await _userBusiness.Login(loginDto);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
    }

    [Fact]
    public async Task RenewTokenAsync_ShouldFail_WhenRefreshTokenNotFound()
    {
        var refreshToken = "invalid-token";
        var ip = "127.0.0.1";

        _tokenProviderMock.Setup(tp => tp.GetRefreshTokenAsync(refreshToken)).ReturnsAsync((RefreshToken)null);

        var result = await _userBusiness.RenewTokenAsync(refreshToken, ip);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RefreshTokenError.RefreshNotFound);
    }

    [Fact]
    public async Task RenewTokenAsync_ShouldSucceed_WhenTokenIsValid()
    {
        var refreshToken = "token";
        var ip = "127.0.0.1";
        var userId = 1;

        var user = new User
        {
            UserId = userId,
            Email = "test@example.com",
            RoleId = (int)RoleEnum.Admin
        };

        var storedToken = new RefreshToken
        {
            Token = refreshToken,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _tokenProviderMock.Setup(tp => tp.GetRefreshTokenAsync(refreshToken)).ReturnsAsync(storedToken);
        _contextMock.Setup(c => c.Users.FindAsync(It.IsAny<object[]>())).ReturnsAsync(user);
        _jwtServiceMock.Setup(j => j.BuildToken(It.IsAny<IEnumerable<Claim>>())).Returns("new-access");
        _tokenProviderMock.Setup(tp => tp.GenerateRefreshToken()).Returns("new-refresh");
        _tokenProviderMock.Setup(tp => tp.SaveRefreshTokenAsync("new-refresh", userId, ip)).Returns(Task.CompletedTask);
        _tokenProviderMock.Setup(tp => tp.RevokeRefreshTokenAsync(refreshToken, ip, It.IsAny<string>())).Returns(Task.CompletedTask);

        _unitOfWorkMock
     .Setup(uow => uow.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<IsolationLevel>()))
     .Returns<Func<Task>, IsolationLevel>((func, _) => func.Invoke());


        var result = await _userBusiness.RenewTokenAsync(refreshToken, ip);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access");
        result.Value.RefreshToken.Should().Be("new-refresh");

        _tokenProviderMock.Verify(tp => tp.SaveRefreshTokenAsync("new-refresh", userId, ip), Times.Once);
        _tokenProviderMock.Verify(tp => tp.RevokeRefreshTokenAsync(refreshToken, ip, It.IsAny<string>()), Times.Once);
        _jwtServiceMock.Verify(j => j.BuildToken(It.IsAny<IEnumerable<Claim>>()), Times.Once);
    }
}
