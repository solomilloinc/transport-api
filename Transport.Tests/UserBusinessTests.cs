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
    private readonly IUserBusiness _userBusiness;
    private Mock<IJwtOption> _jwtOptionMock;

    public UserBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _jwtServiceMock = new Mock<IJwtService>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenProviderMock = new Mock<ITokenProvider>();
        _userBusiness = new UserBusiness(_jwtServiceMock.Object, _contextMock.Object, _passwordHasherMock.Object, _tokenProviderMock.Object);
        _jwtOptionMock = new Mock<IJwtOption>();
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

        var users = new List<User> { user };
        _contextMock.Setup(x => x.Users).Returns(GetQueryableMockDbSet(users).Object);

        _passwordHasherMock.Setup(p => p.Verify("password", "hashedPassword")).Returns(true);

        var refreshTokenList = new List<RefreshToken>();
        _contextMock.Setup(x => x.RefreshTokens).Returns(GetMockDbSetWithIdentity(refreshTokenList).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _jwtServiceMock.Setup(j => j.BuildToken(It.IsAny<IEnumerable<Claim>>()))
            .Returns("access-token");

        _tokenProviderMock.Setup(j => j.GenerateRefreshToken())
            .Returns("refresh-token");

        var jwtServiceMock = _jwtServiceMock.Object; 

        var userBusiness = new UserBusiness(
            jwtServiceMock,  // Aquí usamos el mock del servicio
            _contextMock.Object,
            _passwordHasherMock.Object,
            _tokenProviderMock.Object
        );

        // Act
        var result = await userBusiness.Login(loginDto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be("access-token");  // Verifica que es el token mockeado
        result.Value.RefreshToken.Should().Be("refresh-token");

        _contextMock.Verify(x => x.RefreshTokens.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(x => x.SaveChangesWithOutboxAsync(It.IsAny<CancellationToken>()), Times.Once);
    }




    [Fact]
    public async Task RenewTokenAsync_ShouldFail_WhenRefreshTokenNotFound()
    {
        var refreshToken = "invalid-refresh-token";
        var ipAddress = "127.0.0.1";

        _tokenProviderMock.Setup(tp => tp.GetRefreshTokenAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken)null);

        var result = await _userBusiness.RenewTokenAsync(refreshToken, ipAddress);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RefreshTokenError.RefreshNotFound);
    }

    [Fact]
    public async Task RenewTokenAsync_ShouldSucceed_WhenRefreshTokenIsValid()
    {
        // Arrange
        var refreshToken = "valid-refresh-token";
        var ipAddress = "127.0.0.1";
        var userId = 1;

        var user = new User
        {
            UserId = userId,
            Email = "test@example.com",
            Role = new Role { RoleId = 1, Name = "Admin" }
        };

        var storedRefreshToken = new RefreshToken
        {
            Token = refreshToken,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var newAccessToken = "new-access-token";
        var newRefreshToken = "new-refresh-token";

        _tokenProviderMock
            .Setup(tp => tp.GetRefreshTokenAsync(refreshToken))
            .ReturnsAsync(storedRefreshToken);

        _contextMock
            .Setup(x => x.Users.FindAsync(It.IsAny<object[]>()))
            .ReturnsAsync(user);

        _jwtServiceMock
            .Setup(j => j.BuildToken(It.IsAny<IEnumerable<Claim>>()))
            .Returns(newAccessToken);

        _tokenProviderMock
            .Setup(tp => tp.GenerateRefreshToken())
            .Returns(newRefreshToken);

        _tokenProviderMock
            .Setup(tp => tp.SaveRefreshTokenAsync(newRefreshToken, userId, ipAddress))
            .Returns(Task.CompletedTask);

        _tokenProviderMock
            .Setup(tp => tp.RevokeRefreshTokenAsync(refreshToken, ipAddress))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userBusiness.RenewTokenAsync(refreshToken, ipAddress);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(newAccessToken);
        result.Value.RefreshToken.Should().Be(newRefreshToken);

        _tokenProviderMock.Verify(tp => tp.RevokeRefreshTokenAsync(refreshToken, ipAddress), Times.Once);
        _tokenProviderMock.Verify(tp => tp.SaveRefreshTokenAsync(newRefreshToken, userId, ipAddress), Times.Once);
        _jwtServiceMock.Verify(j => j.BuildToken(It.IsAny<IEnumerable<Claim>>()), Times.Once);
    }

    [Fact]
    public async Task RenewTokenAsync_ShouldFail_WhenRefreshTokenIsInvalid()
    {
        // Arrange
        var refreshToken = "invalid-refresh-token";
        var ipAddress = "127.0.0.1";

        // Simulamos que no se encuentra el token en la base de datos
        _tokenProviderMock
            .Setup(tp => tp.GetRefreshTokenAsync(refreshToken))
            .ReturnsAsync((RefreshToken?)null); // devuelve null

        // Act
        var result = await _userBusiness.RenewTokenAsync(refreshToken, ipAddress);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RefreshTokenError.RefreshNotFound);

        // Verificamos que NO se hayan ejecutado estas acciones
        _jwtServiceMock.Verify(j => j.BuildToken(It.IsAny<IEnumerable<Claim>>()), Times.Never);
        _tokenProviderMock.Verify(tp => tp.SaveRefreshTokenAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _tokenProviderMock.Verify(tp => tp.RevokeRefreshTokenAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

}
