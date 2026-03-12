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
        var tripPickupStops = new List<TripPickupStop>();

        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(x => x.Directions).Returns(GetQueryableMockDbSet(directions).Object);
        _contextMock.Setup(x => x.TripPickupStops).Returns(GetMockDbSetWithIdentity(tripPickupStops).Object);

        var dto = new TripPickupStopCreateDto(
            TripId: 1,
            DirectionId: 10,
            Order: 0,
            PickupTimeOffset: TimeSpan.FromMinutes(30));

        // Act
        var result = await _tripBusiness.AddDirection(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Single(tripPickupStops);
        Assert.Equal(10, tripPickupStops[0].DirectionId);
        Assert.Equal(TimeSpan.FromMinutes(30), tripPickupStops[0].PickupTimeOffset);
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
        var tripPickupStops = new List<TripPickupStop>();

        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(x => x.Directions).Returns(GetQueryableMockDbSet(directions).Object);
        _contextMock.Setup(x => x.TripPickupStops).Returns(GetQueryableMockDbSet(tripPickupStops).Object);

        var dto = new TripPickupStopCreateDto(TripId: 99, DirectionId: 10, Order: 0, PickupTimeOffset: TimeSpan.Zero);

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
        var tripPickupStops = new List<TripPickupStop>
        {
            new TripPickupStop { TripPickupStopId = 1, TripId = 1, DirectionId = 10, Order = 0, PickupTimeOffset = TimeSpan.Zero, Status = EntityStatusEnum.Active }
        };

        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(x => x.Directions).Returns(GetQueryableMockDbSet(directions).Object);
        _contextMock.Setup(x => x.TripPickupStops).Returns(GetQueryableMockDbSet(tripPickupStops).Object);

        var dto = new TripPickupStopCreateDto(TripId: 1, DirectionId: 10, Order: 1, PickupTimeOffset: TimeSpan.FromMinutes(15));

        // Act
        var result = await _tripBusiness.AddDirection(dto);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("TripPickupStop.AlreadyExists", result.Error.Code);
    }

    [Fact]
    public async Task UpdateDirection_ShouldSucceed()
    {
        // Arrange
        var tripPickupStops = new List<TripPickupStop>
        {
            new TripPickupStop { TripPickupStopId = 1, TripId = 1, DirectionId = 10, Order = 0, PickupTimeOffset = TimeSpan.Zero, Status = EntityStatusEnum.Active }
        };
        var directions = new List<Direction>
        {
            new Direction { DirectionId = 10, Name = "Parada Centro", CityId = 1, Status = EntityStatusEnum.Active },
            new Direction { DirectionId = 20, Name = "Parada Norte", CityId = 1, Status = EntityStatusEnum.Active }
        };

        _contextMock.Setup(x => x.TripPickupStops).Returns(GetQueryableMockDbSet(tripPickupStops).Object);
        _contextMock.Setup(x => x.Directions).Returns(GetQueryableMockDbSet(directions).Object);

        var dto = new TripPickupStopUpdateDto(DirectionId: 20, Order: 1, PickupTimeOffset: TimeSpan.FromMinutes(45));

        // Act
        var result = await _tripBusiness.UpdateDirection(1, dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(20, tripPickupStops[0].DirectionId);
        Assert.Equal(1, tripPickupStops[0].Order);
        Assert.Equal(TimeSpan.FromMinutes(45), tripPickupStops[0].PickupTimeOffset);
    }

    [Fact]
    public async Task DeleteDirection_ShouldSucceed()
    {
        // Arrange
        var tripPickupStops = new List<TripPickupStop>
        {
            new TripPickupStop { TripPickupStopId = 1, TripId = 1, DirectionId = 10, Order = 0, PickupTimeOffset = TimeSpan.Zero, Status = EntityStatusEnum.Active }
        };

        _contextMock.Setup(x => x.TripPickupStops).Returns(GetQueryableMockDbSet(tripPickupStops).Object);

        // Act
        var result = await _tripBusiness.DeleteDirection(1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EntityStatusEnum.Deleted, tripPickupStops[0].Status);
    }
}
