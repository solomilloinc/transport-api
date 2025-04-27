using FluentAssertions;
using Moq;
using Transport.Domain.Cities;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.City;
using Transport.Business.CityBusiness;
using Transport.Domain.Cities.Abstraction;
using Transport.Business.Data;
using Xunit;
using Transport.Domain.Vehicles;

namespace Transport.Tests.CityBusinessTests;

public class CityBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ICityBusiness _CityBusiness;

    public CityBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _CityBusiness = new CityBusiness(_contextMock.Object);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenCityAlreadyExists()
    {
        var existing = new City { CityId = 1, Code = "123", Name = "test" };
        _contextMock.Setup(x => x.Cities)
            .Returns(GetQueryableMockDbSet(new List<City> { existing }).Object);

        var dto = new CityCreateRequestDto("123", "test");

        var result = await _CityBusiness.Create(dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CityError.CityAlreadyExist);
    }

    [Fact]
    public async Task Create_ShouldSucceed_WhenDataIsValid()
    {
        var Cities = new List<City>();
        _contextMock.Setup(x => x.Cities).Returns(GetMockDbSetWithIdentity(Cities).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new CityCreateRequestDto("ABC", "test");

        var result = await _CityBusiness.Create(dto);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Delete_ShouldFail_WhenNotFound()
    {
        _contextMock.Setup(x => x.Cities)
            .Returns(GetQueryableMockDbSet(new List<City>()).Object);

        var result = await _CityBusiness.Delete(1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CityError.CityNotFound);
    }

    [Fact]
    public async Task Delete_ShouldSucceed_WhenCityExists()
    {
        var city = new City { CityId = 1, Status = EntityStatusEnum.Active };
        _contextMock.Setup(x => x.Cities)
            .Returns(GetQueryableMockDbSet(new List<City> { city }).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _CityBusiness.Delete(1);

        result.IsSuccess.Should().BeTrue();
        city.Status.Should().Be(EntityStatusEnum.Deleted);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenNotFound()
    {
        _contextMock.Setup(x => x.Cities)
            .Returns(GetQueryableMockDbSet(new List<City>()).Object);

        var dto = new CityUpdateRequestDto("222", "test");
        var result = await _CityBusiness.Update(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CityError.CityNotFound);
    }

    [Fact]
    public async Task Update_ShouldSucceed_WhenFound()
    {
        var City = new City { CityId = 1, Code = "123", Name = "test" };
        _contextMock.Setup(x => x.Cities)
            .Returns(GetQueryableMockDbSet(new List<City> { City }).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new CityUpdateRequestDto("222", "test");
        var result = await _CityBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        City.Code.Should().Be("222");
    }

    [Fact]
    public async Task UpdateStatus_ShouldFail_WhenNotFound()
    {
        _contextMock.Setup(x => x.Cities)
            .Returns(GetQueryableMockDbSet(new List<City>()).Object);

        var result = await _CityBusiness.UpdateStatus(9, EntityStatusEnum.Inactive);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CityError.CityNotFound);
    }

    [Fact]
    public async Task UpdateStatus_ShouldSucceed_WhenCityExists()
    {
        var City = new City { CityId = 1, Status = EntityStatusEnum.Active };
        _contextMock.Setup(x => x.Cities)
            .Returns(GetQueryableMockDbSet(new List<City> { City }).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _CityBusiness.UpdateStatus(1, EntityStatusEnum.Inactive);

        result.IsSuccess.Should().BeTrue();
        City.Status.Should().Be(EntityStatusEnum.Inactive);
    }
}