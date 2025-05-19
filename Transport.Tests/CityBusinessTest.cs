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
using Transport.Domain;

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

        var dto = new CityCreateRequestDto("123", "test", null);

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

        var dto = new CityCreateRequestDto("ABC", "test", null);

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

        var dto = new CityUpdateRequestDto("222", "test", null);
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

        var dto = new CityUpdateRequestDto("222", "test", null);
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

    [Fact]
    public async Task GetCityReport_ShouldReturnFilteredPagedResultWithDirections()
    {
        // Arrange
        var cities = new List<City>
    {
        new City
        {
            CityId = 1,
            Name = "Buenos Aires",
            Code = "BA",
            Status = EntityStatusEnum.Active,
            Directions = new List<Direction>
            {
                new Direction { DirectionId = 1, Name = "Calle 1", Lat = -34.6037, Lng = -58.3816 },
                new Direction { DirectionId = 2, Name = "Calle 2", Lat = -34.6040, Lng = -58.3820 }
            }
        },
        new City
        {
            CityId = 2,
            Name = "Córdoba",
            Code = "CB",
            Status = EntityStatusEnum.Active,
            Directions = new List<Direction>()
        }
    };

        _contextMock.Setup(x => x.Cities).Returns(GetQueryableMockDbSet(cities).Object);

        var requestDto = new PagedReportRequestDto<CityReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new CityReportFilterRequestDto("Buenos Aires", "BA", EntityStatusEnum.Active, true),
            SortBy = "name",
            SortDescending = false
        };

        // Act
        var result = await _CityBusiness.GetReport(requestDto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);

        var cityDto = result.Value.Items.First();
        cityDto.Name.Should().Be("Buenos Aires");
        cityDto.Code.Should().Be("BA");
        cityDto.Directions.Should().HaveCount(2);
        cityDto.Directions.First().Name.Should().Be("Calle 1");
    }

    [Fact]
    public async Task Create_ShouldSucceed_WithDirections()
    {
        var cities = new List<City>();
        _contextMock.Setup(x => x.Cities).Returns(GetMockDbSetWithIdentity(cities).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var directions = new List<DirectionCreateRequestDto>
        {
            new("Avenida Siempre Viva", -34.60, -58.38),
            new("Calle Falsa 123", -34.61, -58.39)
        };

        var dto = new CityCreateRequestDto("CBA", "Córdoba", directions);

        var result = await _CityBusiness.Create(dto);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);

        var created = cities.First();
        created.Directions.Should().HaveCount(2);
        created.Directions.First().Name.Should().Be("Avenida Siempre Viva");
    }

    [Fact]
    public async Task Update_ShouldSucceed_AndReplaceDirections()
    {
        var city = new City
        {
            CityId = 1,
            Code = "OLD",
            Name = "Old City",
            Directions = new List<Direction>
        {
            new Direction { Name = "Old Street", Lat = -1, Lng = -1 }
        }
        };

        var cities = new List<City> { city };
        _contextMock.Setup(x => x.Cities)
            .Returns(GetQueryableMockDbSet(cities).Object);

        _contextMock.Setup(x => x.Cities.FindAsync(1))
            .ReturnsAsync(city);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var newDirections = new List<DirectionCreateRequestDto>
    {
        new("New Street", 12.34, 56.78),
        new("Second Street", 21.43, 65.87)
    };

        var dto = new CityUpdateRequestDto("NEW", "New City", newDirections);

        var result = await _CityBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        city.Code.Should().Be("NEW");
        city.Name.Should().Be("New City");
        city.Directions.Should().HaveCount(2);
        city.Directions.Any(d => d.Name == "Old Street").Should().BeFalse();
    }

}