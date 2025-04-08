using FluentAssertions;
using Moq;
using transport.domain.Drivers;
using Transport.Business.Data;
using Transport.Business.DriverBusiness;
using Transport.Domain.Drivers;
using Transport.Tests;
using Xunit;

namespace Transport.Tests.DriverBusinessTests;

public class DriverBusinessTests: TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly IDriverBusiness _driverBusiness;

    public DriverBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _driverBusiness = new DriverBusiness(_unitOfWorkMock.Object, _contextMock.Object);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenDriverAlreadyExists()
    {
        // Arrange
        var existingDriver = new Driver { DocumentNumber = "12345678" };

        var driversDbSetMock = GetQueryableMockDbSet(new List<Driver> { existingDriver });
        _contextMock.Setup(x => x.Drivers).Returns(driversDbSetMock.Object);

        var dto = new DriverCreateRequestDto("Juan", "Pérez", "12345678");

        // Act
        var result = await _driverBusiness.Create(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DriverError.DriverAlreadyExist);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenDocumentIsBlacklisted()
    {
        // Arrange
        var driversDbSetMock = GetQueryableMockDbSet(new List<Driver>());
        _contextMock.Setup(x => x.Drivers).Returns(driversDbSetMock.Object);

        var dto = new DriverCreateRequestDto("Pedro", "Lopez", "37976806");

        // Act
        var result = await _driverBusiness.Create(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DriverError.EmailInBlackList);
    }

    [Fact]
    public async Task Create_ShouldSucceed_WhenValidData()
    {
        var drivers = new List<Driver>();
        var driversDbSetMock = GetMockDbSetWithIdentity(drivers);
        _contextMock.Setup(x => x.Drivers).Returns(driversDbSetMock.Object);

        _contextMock.Setup(x => x.SaveChangesWithOutboxAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dto = new DriverCreateRequestDto("Ana", "Gonzalez", "5551234");

        // Act
        var result = await _driverBusiness.Create(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Create_ShouldRaiseEvent_WhenDriverIsCreated()
    {
        // Arrange
        var drivers = new List<Driver>();
        _contextMock.Setup(x => x.Drivers).Returns(GetQueryableMockDbSet(drivers).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new DriverCreateRequestDto("Test", "User", "22222222");

        // Act
        var result = await _driverBusiness.Create(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        drivers.Count.Should().Be(1);

        var createdDriver = drivers.First();
        var raisedEvent = GetRaisedEvent<Driver, DriverCreatedEvent>(createdDriver);

        raisedEvent.Should().NotBeNull();
        raisedEvent!.DriverId.Should().Be(result.Value);
    }
}