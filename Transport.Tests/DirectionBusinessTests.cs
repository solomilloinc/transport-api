using Moq;
using Transport.Business.Data;
using Transport.Business.DirectionBusiness;
using Transport.Domain.Directions;
using Transport.SharedKernel.Contracts.Direction;
using Transport.SharedKernel;
using Xunit;
using FluentAssertions;

namespace Transport.Tests.DirectionBusinessTests;

public class DirectionBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly DirectionBusiness _directionBusiness;

    public DirectionBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _directionBusiness = new DirectionBusiness(_contextMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldSucceed_WhenValid()
    {
        var directions = new List<Direction>();
        _contextMock.Setup(x => x.Directions).Returns(GetMockDbSetWithIdentity(directions).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new DirectionCreateDto("Calle 123", -34.60, -58.38, 1);

        var result = await _directionBusiness.CreateAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenDirectionNotFound()
    {
        _contextMock.Setup(x => x.Directions.FindAsync(It.IsAny<object[]>()))
            .ReturnsAsync((Direction?)null);

        var dto = new DirectionUpdateDto("Calle Nueva", -34.60, -58.38, 2);
        var result = await _directionBusiness.UpdateAsync(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DirectionError.DirectionNotFound);
    }

    [Fact]
    public async Task UpdateAsync_ShouldSucceed_WhenDirectionExists()
    {
        var direction = new Direction { DirectionId = 1, Name = "Vieja", Lat = 0, Lng = 0, CityId = 1 };
        _contextMock.Setup(x => x.Directions.FindAsync(1)).ReturnsAsync(direction);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new DirectionUpdateDto("Nueva", -34.61, -58.39, 2);
        var result = await _directionBusiness.UpdateAsync(1, dto);

        result.IsSuccess.Should().BeTrue();
        direction.Name.Should().Be("Nueva");
        direction.Lat.Should().Be(-34.61);
        direction.Lng.Should().Be(-58.39);
        direction.CityId.Should().Be(2);
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenDirectionNotFound()
    {
        _contextMock.Setup(x => x.Directions.FindAsync(It.IsAny<object[]>()))
            .ReturnsAsync((Direction?)null);

        var result = await _directionBusiness.DeleteAsync(1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DirectionError.DirectionNotFound);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSucceed_WhenDirectionExists()
    {
        var direction = new Direction { DirectionId = 1, Status = EntityStatusEnum.Active };
        _contextMock.Setup(x => x.Directions.FindAsync(1)).ReturnsAsync(direction);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _directionBusiness.DeleteAsync(1);

        result.IsSuccess.Should().BeTrue();
        direction.Status.Should().Be(EntityStatusEnum.Deleted);
    }
}
