using FluentAssertions;
using Moq;
using Transport.Business.Data;
using Transport.Business.TenantReserveConfigBusiness;
using Transport.Domain.Tenants;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Tenant;
using Xunit;

namespace Transport.Tests;

public class TenantReserveConfigBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly TenantReserveConfigBusiness _business;

    public TenantReserveConfigBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _business = new TenantReserveConfigBusiness(_contextMock.Object);
    }

    [Fact]
    public async Task Get_ShouldReturnDefault_WhenNoExplicitConfigExists()
    {
        var tenant = new Tenant { TenantId = 1, Code = "x", Name = "X" };
        _contextMock.Setup(c => c.Tenants).Returns(GetQueryableMockDbSet(new List<Tenant> { tenant }));
        _contextMock.Setup(c => c.TenantReserveConfigs).Returns(GetQueryableMockDbSet(new List<TenantReserveConfig>()));

        var result = await _business.Get(1);

        result.IsSuccess.Should().BeTrue();
        result.Value.RoundTripSameDayOnly.Should().BeTrue();
        result.Value.TenantId.Should().Be(1);
    }

    [Fact]
    public async Task Get_ShouldReturnPersistedValue_WhenConfigExists()
    {
        var tenant = new Tenant { TenantId = 1, Code = "x", Name = "X" };
        var existing = new TenantReserveConfig { TenantId = 1, RoundTripSameDayOnly = false };
        _contextMock.Setup(c => c.Tenants).Returns(GetQueryableMockDbSet(new List<Tenant> { tenant }));
        _contextMock.Setup(c => c.TenantReserveConfigs).Returns(GetQueryableMockDbSet(new List<TenantReserveConfig> { existing }));

        var result = await _business.Get(1);

        result.IsSuccess.Should().BeTrue();
        result.Value.RoundTripSameDayOnly.Should().BeFalse();
    }

    [Fact]
    public async Task Get_ShouldFail_WhenTenantNotFound()
    {
        _contextMock.Setup(c => c.Tenants).Returns(GetQueryableMockDbSet(new List<Tenant>()));

        var result = await _business.Get(99);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(TenantError.NotFound.Code);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenTenantNotFound()
    {
        _contextMock.Setup(c => c.Tenants).Returns(GetQueryableMockDbSet(new List<Tenant>()));

        var result = await _business.Update(99, new TenantReserveConfigUpdateRequestDto(true));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(TenantError.NotFound.Code);
    }

    [Fact]
    public async Task Update_ShouldCreateNewConfig_WhenNoneExists()
    {
        var tenant = new Tenant { TenantId = 1, Code = "x", Name = "X" };
        var configs = new List<TenantReserveConfig>();
        _contextMock.Setup(c => c.Tenants).Returns(GetQueryableMockDbSet(new List<Tenant> { tenant }));
        _contextMock.Setup(c => c.TenantReserveConfigs).Returns(GetMockDbSetWithIdentity(configs));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _business.Update(1, new TenantReserveConfigUpdateRequestDto(false));

        result.IsSuccess.Should().BeTrue();
        result.Value.RoundTripSameDayOnly.Should().BeFalse();
        configs.Should().ContainSingle(c => c.TenantId == 1 && !c.RoundTripSameDayOnly);
    }

    [Fact]
    public async Task Update_ShouldOverwriteExistingConfig()
    {
        var tenant = new Tenant { TenantId = 1, Code = "x", Name = "X" };
        var existing = new TenantReserveConfig { TenantReserveConfigId = 5, TenantId = 1, RoundTripSameDayOnly = true };
        _contextMock.Setup(c => c.Tenants).Returns(GetQueryableMockDbSet(new List<Tenant> { tenant }));
        _contextMock.Setup(c => c.TenantReserveConfigs).Returns(GetQueryableMockDbSet(new List<TenantReserveConfig> { existing }));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _business.Update(1, new TenantReserveConfigUpdateRequestDto(false));

        result.IsSuccess.Should().BeTrue();
        existing.RoundTripSameDayOnly.Should().BeFalse();
    }
}
