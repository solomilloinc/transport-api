using Moq;
using Transport.Business.Data;
using Transport.Business.TripBusiness;
using Transport.Domain.Directions;
using Transport.Domain.Trips;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Trip;
using Xunit;

namespace Transport.Tests;

public class TripBusinessTest : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly TripBusiness _tripBusiness;

    public TripBusinessTest()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        SetupSaveChangesWithOutboxAsync(_contextMock);
        _tripBusiness = new TripBusiness(_contextMock.Object);
    }

    [Fact]
    public async Task AddDirection_ShouldSucceed_WhenValidInput()
    {
        // Arrange
        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active }
        };
        var directions = new List<Direction>
        {
            new Direction { DirectionId = 10, Name = "Parada Centro", CityId = 1, Status = EntityStatusEnum.Active }
        };
        var tripDirections = new List<TripDirection>();

        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(x => x.Directions).Returns(GetQueryableMockDbSet(directions).Object);
        _contextMock.Setup(x => x.TripDirections).Returns(GetMockDbSetWithIdentity(tripDirections).Object);

        var dto = new TripDirectionCreateDto(
            TripId: 1,
            DirectionId: 10,
            Order: 0,
            PickupTimeOffset: TimeSpan.FromMinutes(30));

        // Act
        var result = await _tripBusiness.AddDirection(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Single(tripDirections);
        Assert.Equal(10, tripDirections[0].DirectionId);
        Assert.Equal(TimeSpan.FromMinutes(30), tripDirections[0].PickupTimeOffset);
    }

    [Fact]
    public async Task AddDirection_ShouldFail_WhenTripNotFound()
    {
        // Arrange
        var trips = new List<Trip>();
        var directions = new List<Direction>
        {
            new Direction { DirectionId = 10, Name = "Parada Centro", CityId = 1, Status = EntityStatusEnum.Active }
        };
        var tripDirections = new List<TripDirection>();

        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(x => x.Directions).Returns(GetQueryableMockDbSet(directions).Object);
        _contextMock.Setup(x => x.TripDirections).Returns(GetQueryableMockDbSet(tripDirections).Object);

        var dto = new TripDirectionCreateDto(TripId: 99, DirectionId: 10, Order: 0, PickupTimeOffset: TimeSpan.Zero);

        // Act
        var result = await _tripBusiness.AddDirection(dto);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Trip.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddDirection_ShouldFail_WhenDuplicate()
    {
        // Arrange
        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active }
        };
        var directions = new List<Direction>
        {
            new Direction { DirectionId = 10, Name = "Parada Centro", CityId = 1, Status = EntityStatusEnum.Active }
        };
        var tripDirections = new List<TripDirection>
        {
            new TripDirection { TripDirectionId = 1, TripId = 1, DirectionId = 10, Order = 0, PickupTimeOffset = TimeSpan.Zero, Status = EntityStatusEnum.Active }
        };

        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(x => x.Directions).Returns(GetQueryableMockDbSet(directions).Object);
        _contextMock.Setup(x => x.TripDirections).Returns(GetQueryableMockDbSet(tripDirections).Object);

        var dto = new TripDirectionCreateDto(TripId: 1, DirectionId: 10, Order: 1, PickupTimeOffset: TimeSpan.FromMinutes(15));

        // Act
        var result = await _tripBusiness.AddDirection(dto);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("TripDirection.AlreadyExists", result.Error.Code);
    }

    [Fact]
    public async Task UpdateDirection_ShouldSucceed()
    {
        // Arrange
        var tripDirections = new List<TripDirection>
        {
            new TripDirection { TripDirectionId = 1, TripId = 1, DirectionId = 10, Order = 0, PickupTimeOffset = TimeSpan.Zero, Status = EntityStatusEnum.Active }
        };
        var directions = new List<Direction>
        {
            new Direction { DirectionId = 10, Name = "Parada Centro", CityId = 1, Status = EntityStatusEnum.Active },
            new Direction { DirectionId = 20, Name = "Parada Norte", CityId = 1, Status = EntityStatusEnum.Active }
        };

        _contextMock.Setup(x => x.TripDirections).Returns(GetQueryableMockDbSet(tripDirections).Object);
        _contextMock.Setup(x => x.Directions).Returns(GetQueryableMockDbSet(directions).Object);

        var dto = new TripDirectionUpdateDto(DirectionId: 20, Order: 1, PickupTimeOffset: TimeSpan.FromMinutes(45));

        // Act
        var result = await _tripBusiness.UpdateDirection(1, dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(20, tripDirections[0].DirectionId);
        Assert.Equal(1, tripDirections[0].Order);
        Assert.Equal(TimeSpan.FromMinutes(45), tripDirections[0].PickupTimeOffset);
    }

    [Fact]
    public async Task DeleteDirection_ShouldSucceed()
    {
        // Arrange
        var tripDirections = new List<TripDirection>
        {
            new TripDirection { TripDirectionId = 1, TripId = 1, DirectionId = 10, Order = 0, PickupTimeOffset = TimeSpan.Zero, Status = EntityStatusEnum.Active }
        };

        _contextMock.Setup(x => x.TripDirections).Returns(GetQueryableMockDbSet(tripDirections).Object);

        // Act
        var result = await _tripBusiness.DeleteDirection(1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EntityStatusEnum.Deleted, tripDirections[0].Status);
    }
}
