using System.Security.Claims;
using FluentAssertions;
using Moq;
using Transport.Business.Authentication;
using Transport.Business.Authorization;
using Transport.Business.Data;
using Transport.Business.TenantBusiness;
using Transport.Business.UserBusiness;
using Transport.Domain.Cities;
using Transport.Domain.Customers;
using Transport.Domain.Tenants;
using Transport.Domain.Tenants.Abstraction;
using Transport.Domain.Users;
using Transport.Domain.Users.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Tenant;
using Transport.SharedKernel.Contracts.User;
using Xunit;

namespace Transport.Tests;

public class MultiTenantTests : TestBase
{
    // ──────────────────────────────────────────────
    // 1. Login Flow - Tenant Isolation
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Login_ShouldFail_WhenUserBelongsToDifferentTenant()
    {
        // User exists in tenant 2, but login request is for tenant 1
        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.TenantId).Returns(1);

        var contextMock = new Mock<IApplicationDbContext>();
        var jwtServiceMock = new Mock<IJwtService>();
        var passwordHasherMock = new Mock<IPasswordHasher>();
        var tokenProviderMock = new Mock<ITokenProvider>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        var user = new User
        {
            UserId = 1,
            Email = "admin@empresa2.com",
            Password = "hashedPassword",
            TenantId = 2, // Different tenant
            Role = new Role { RoleId = 1, Name = "Admin" }
        };

        // The user exists but belongs to tenant 2; tenant-aware query filters by TenantId == 1
        contextMock.Setup(x => x.Users)
            .Returns(GetQueryableMockDbSet(new List<User> { user }));

        passwordHasherMock.Setup(p => p.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var userBusiness = new UserBusiness(
            jwtServiceMock.Object,
            contextMock.Object,
            passwordHasherMock.Object,
            tokenProviderMock.Object,
            unitOfWorkMock.Object,
            tenantContextMock.Object
        );

        var loginDto = new LoginDto("admin@empresa2.com", "password", "127.0.0.1");

        Func<Task> action = async () => await userBusiness.Login(loginDto);

        // Should fail because UserBusiness.Login filters by u.TenantId == tenantContext.TenantId (1)
        // and the only user has TenantId == 2
        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task Login_ShouldSucceed_WhenUserBelongsToSameTenant()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.TenantId).Returns(1);

        var contextMock = new Mock<IApplicationDbContext>();
        var jwtServiceMock = new Mock<IJwtService>();
        var passwordHasherMock = new Mock<IPasswordHasher>();
        var tokenProviderMock = new Mock<ITokenProvider>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        var user = new User
        {
            UserId = 1,
            Email = "admin@empresa1.com",
            Password = "hashedPassword",
            TenantId = 1, // Same tenant
            Role = new Role { RoleId = 1, Name = "Admin" }
        };

        contextMock.Setup(x => x.Users)
            .Returns(GetQueryableMockDbSet(new List<User> { user }));

        passwordHasherMock.Setup(p => p.Verify("password", "hashedPassword")).Returns(true);
        jwtServiceMock.Setup(j => j.BuildToken(It.IsAny<IEnumerable<Claim>>())).Returns("access-token");
        tokenProviderMock.Setup(tp => tp.GenerateRefreshToken()).Returns("refresh-token");
        tokenProviderMock.Setup(tp => tp.SaveRefreshTokenAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var userBusiness = new UserBusiness(
            jwtServiceMock.Object,
            contextMock.Object,
            passwordHasherMock.Object,
            tokenProviderMock.Object,
            unitOfWorkMock.Object,
            tenantContextMock.Object
        );

        var loginDto = new LoginDto("admin@empresa1.com", "password", "127.0.0.1");

        var result = await userBusiness.Login(loginDto);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
    }

    [Fact]
    public async Task Login_ShouldIncludeTenantIdClaim_InJwt()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.TenantId).Returns(5);

        var contextMock = new Mock<IApplicationDbContext>();
        var jwtServiceMock = new Mock<IJwtService>();
        var passwordHasherMock = new Mock<IPasswordHasher>();
        var tokenProviderMock = new Mock<ITokenProvider>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        var user = new User
        {
            UserId = 10,
            Email = "user@empresa5.com",
            Password = "hashed",
            TenantId = 5,
            Role = new Role { RoleId = 1, Name = "Admin" }
        };

        contextMock.Setup(x => x.Users)
            .Returns(GetQueryableMockDbSet(new List<User> { user }));

        passwordHasherMock.Setup(p => p.Verify("pass", "hashed")).Returns(true);

        IEnumerable<Claim>? capturedClaims = null;
        jwtServiceMock.Setup(j => j.BuildToken(It.IsAny<IEnumerable<Claim>>()))
            .Callback<IEnumerable<Claim>>(c => capturedClaims = c)
            .Returns("token");
        tokenProviderMock.Setup(tp => tp.GenerateRefreshToken()).Returns("refresh");
        tokenProviderMock.Setup(tp => tp.SaveRefreshTokenAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var userBusiness = new UserBusiness(
            jwtServiceMock.Object,
            contextMock.Object,
            passwordHasherMock.Object,
            tokenProviderMock.Object,
            unitOfWorkMock.Object,
            tenantContextMock.Object
        );

        await userBusiness.Login(new LoginDto("user@empresa5.com", "pass", "127.0.0.1"));

        capturedClaims.Should().NotBeNull();
        capturedClaims!.Should().Contain(c => c.Type == "tenant_id" && c.Value == "5");
    }

    // ──────────────────────────────────────────────
    // 2. ClaimBuilder - TenantId Claim
    // ──────────────────────────────────────────────

    [Fact]
    public void ClaimBuilder_ShouldIncludeTenantIdClaim()
    {
        var claims = ClaimBuilder.Create()
            .SetEmail("test@test.com")
            .SetRole("Admin")
            .SetId("1")
            .SetTenantId(42)
            .Build();

        claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == "42");
    }

    [Fact]
    public void ClaimBuilder_ShouldIncludeAllClaims()
    {
        var claims = ClaimBuilder.Create()
            .SetEmail("user@test.com")
            .SetRole("User")
            .SetId("5")
            .SetTenantId(3)
            .Build();

        claims.Should().HaveCount(4);
        claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "user@test.com");
        claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "5");
        claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == "3");
    }

    // ──────────────────────────────────────────────
    // 3. TenantBusiness - CRUD
    // ──────────────────────────────────────────────

    [Fact]
    public async Task CreateTenant_ShouldSucceed_WhenCodeIsUnique()
    {
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.Tenants)
            .Returns(GetMockDbSetWithIdentity(new List<Tenant>()));
        SetupSaveChangesWithOutboxAsync(contextMock);

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext());

        var result = await business.Create(new TenantCreateRequestDto("newco", "New Company", null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("newco");
        result.Value.Name.Should().Be("New Company");
    }

    [Fact]
    public async Task CreateTenant_ShouldFail_WhenCodeAlreadyExists()
    {
        var existing = new Tenant { TenantId = 1, Code = "existing", Name = "Existing", Status = EntityStatusEnum.Active };
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.Tenants)
            .Returns(GetQueryableMockDbSet(new List<Tenant> { existing }));

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext());

        var result = await business.Create(new TenantCreateRequestDto("existing", "Another", null));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(TenantError.CodeAlreadyExists);
    }

    [Fact]
    public async Task CreateTenant_ShouldFail_WhenDomainAlreadyExists()
    {
        var existing = new Tenant { TenantId = 1, Code = "co1", Name = "Co1", Domain = "co1.com", Status = EntityStatusEnum.Active };
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.Tenants)
            .Returns(GetQueryableMockDbSet(new List<Tenant> { existing }));

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext());

        var result = await business.Create(new TenantCreateRequestDto("co2", "Co2", "co1.com"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(TenantError.DomainAlreadyExists);
    }

    [Fact]
    public async Task DeleteTenant_ShouldSoftDelete()
    {
        var tenant = new Tenant { TenantId = 1, Code = "todelete", Name = "To Delete", Status = EntityStatusEnum.Active };
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.Tenants)
            .Returns(GetQueryableMockDbSet(new List<Tenant> { tenant }));
        SetupSaveChangesWithOutboxAsync(contextMock);

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext());

        var result = await business.Delete(1);

        result.IsSuccess.Should().BeTrue();
        tenant.Status.Should().Be(EntityStatusEnum.Deleted);
    }

    [Fact]
    public async Task UpdateTenant_ShouldFail_WhenNotFound()
    {
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.Tenants)
            .Returns(GetQueryableMockDbSet(new List<Tenant>()));

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext());

        var result = await business.Update(999, new TenantUpdateRequestDto("New Name", null));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(TenantError.NotFound);
    }

    [Fact]
    public async Task GetAllTenants_ShouldExcludeDeleted()
    {
        var tenants = new List<Tenant>
        {
            new() { TenantId = 1, Code = "active1", Name = "Active 1", Status = EntityStatusEnum.Active },
            new() { TenantId = 2, Code = "active2", Name = "Active 2", Status = EntityStatusEnum.Active },
            new() { TenantId = 3, Code = "deleted", Name = "Deleted", Status = EntityStatusEnum.Deleted },
        };
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.Tenants)
            .Returns(GetQueryableMockDbSet(tenants));

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext());

        var result = await business.GetAll();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().NotContain(t => t.Code == "deleted");
    }

    // ──────────────────────────────────────────────
    // 4. TenantBusiness - Payment Config
    // ──────────────────────────────────────────────

    [Fact]
    public async Task UpdatePaymentConfig_ShouldCreateNew_WhenNoneExists()
    {
        var tenant = new Tenant { TenantId = 1, Code = "co", Name = "Co", Status = EntityStatusEnum.Active };
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.Tenants)
            .Returns(GetQueryableMockDbSet(new List<Tenant> { tenant }));
        contextMock.Setup(x => x.TenantPaymentConfigs)
            .Returns(GetMockDbSetWithIdentity(new List<TenantPaymentConfig>()));
        SetupSaveChangesWithOutboxAsync(contextMock);

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext());

        var result = await business.UpdatePaymentConfig(1,
            new TenantPaymentConfigUpdateRequestDto("ACCESS_TOKEN", "PUB_KEY", "WEBHOOK_SECRET"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePaymentConfig_ShouldUpdate_WhenAlreadyExists()
    {
        var tenant = new Tenant { TenantId = 1, Code = "co", Name = "Co", Status = EntityStatusEnum.Active };
        var existingConfig = new TenantPaymentConfig
        {
            TenantPaymentConfigId = 1,
            TenantId = 1,
            AccessToken = "OLD_TOKEN",
            PublicKey = "OLD_KEY",
            Status = EntityStatusEnum.Active
        };
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.Tenants)
            .Returns(GetQueryableMockDbSet(new List<Tenant> { tenant }));
        contextMock.Setup(x => x.TenantPaymentConfigs)
            .Returns(GetQueryableMockDbSet(new List<TenantPaymentConfig> { existingConfig }));
        SetupSaveChangesWithOutboxAsync(contextMock);

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext());

        var result = await business.UpdatePaymentConfig(1,
            new TenantPaymentConfigUpdateRequestDto("NEW_TOKEN", "NEW_KEY", "NEW_SECRET"));

        result.IsSuccess.Should().BeTrue();
        existingConfig.AccessToken.Should().Be("NEW_TOKEN");
        existingConfig.PublicKey.Should().Be("NEW_KEY");
        existingConfig.WebhookSecret.Should().Be("NEW_SECRET");
    }

    [Fact]
    public async Task UpdatePaymentConfig_ShouldFail_WhenTenantNotFound()
    {
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.Tenants)
            .Returns(GetQueryableMockDbSet(new List<Tenant>()));

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext());

        var result = await business.UpdatePaymentConfig(999,
            new TenantPaymentConfigUpdateRequestDto("TOKEN", "KEY", null));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(TenantError.NotFound);
    }

    // ──────────────────────────────────────────────
    // 5. TenantBusiness - Public Config
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetTenantConfig_ShouldReturnComposedJson_WhenTenantExists()
    {
        var config = new TenantConfig
        {
            TenantConfigId = 1,
            TenantId = 1,
            CompanyName = "Empresa 1",
            CompanyNameShort = "E1",
            ContactEmail = "info@empresa1.com",
            StyleConfigJson = "{\"theme\":{\"light\":{\"primary\":\"221 83% 53%\"}},\"contact\":{\"schedule\":[\"Lun-Vie: 8-20\"]}}"
        };
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.TenantConfigs)
            .Returns(GetQueryableMockDbSet(new List<TenantConfig> { config }));

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext { TenantId = 1 });

        var result = await business.GetTenantConfig();

        result.IsSuccess.Should().BeTrue();
        // Structured fields merged into JSON
        result.Value.Should().Contain("\"companyName\":\"Empresa 1\"");
        result.Value.Should().Contain("\"companyNameShort\":\"E1\"");
        result.Value.Should().Contain("\"email\":\"info@empresa1.com\"");
        // Style fields preserved
        result.Value.Should().Contain("\"primary\":\"221 83% 53%\"");
        // contact.schedule from JSON preserved alongside structured contact fields
        result.Value.Should().Contain("\"schedule\":[\"Lun-Vie: 8-20\"]");
    }

    [Fact]
    public async Task GetTenantConfig_ShouldFail_WhenTenantNotFound()
    {
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.TenantConfigs)
            .Returns(GetQueryableMockDbSet(new List<TenantConfig>()));

        var business = new TenantBusiness(contextMock.Object, new FakeTenantContext { TenantId = 999 });

        var result = await business.GetTenantConfig();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(TenantError.NotFound);
    }

    // ──────────────────────────────────────────────
    // 6. SuperAdmin Role
    // ──────────────────────────────────────────────

    [Fact]
    public void SuperAdmin_ShouldExistInRoleEnum()
    {
        var superAdmin = RoleEnum.SuperAdmin;
        ((int)superAdmin).Should().Be(3);
        superAdmin.ToString().Should().Be("SuperAdmin");
    }

    // ──────────────────────────────────────────────
    // 7. FakeTenantContext
    // ──────────────────────────────────────────────

    [Fact]
    public void FakeTenantContext_ShouldHaveDefaultValues()
    {
        var ctx = new FakeTenantContext();
        ctx.TenantId.Should().Be(1);
        ctx.TenantCode.Should().Be("default");
    }

    [Fact]
    public void FakeTenantContext_ShouldAllowOverride()
    {
        var ctx = new FakeTenantContext { TenantId = 5, TenantCode = "empresa5" };
        ctx.TenantId.Should().Be(5);
        ctx.TenantCode.Should().Be("empresa5");
    }

    // ──────────────────────────────────────────────
    // 8. ITenantScoped on entities
    // ──────────────────────────────────────────────

    [Fact]
    public void City_ShouldImplementITenantScoped()
    {
        var city = new City { CityId = 1, TenantId = 5, Code = "BA", Name = "Buenos Aires" };
        (city is ITenantScoped).Should().BeTrue();
        ((ITenantScoped)city).TenantId.Should().Be(5);
    }

    [Fact]
    public void Customer_ShouldImplementITenantScoped()
    {
        var customer = new Customer { CustomerId = 1, TenantId = 3 };
        (customer is ITenantScoped).Should().BeTrue();
        ((ITenantScoped)customer).TenantId.Should().Be(3);
    }

    [Fact]
    public void Tenant_ShouldNotImplementITenantScoped()
    {
        var tenant = new Tenant { TenantId = 1, Code = "x", Name = "X" };
        (tenant is ITenantScoped).Should().BeFalse();
    }

    [Fact]
    public void User_ShouldNotImplementITenantScoped()
    {
        // User has TenantId but NOT ITenantScoped (filtered explicitly in login)
        var user = new User { UserId = 1, TenantId = 2 };
        (user is ITenantScoped).Should().BeFalse();
        user.TenantId.Should().Be(2);
    }
}
