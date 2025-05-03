using FluentAssertions;
using Moq;
using Transport.Domain;
using Transport.Domain.Drivers;
using Transport.Business.Data;
using Transport.Business.DriverBusiness;
using Transport.Domain.Drivers.Abstraction;
using Transport.Domain.Reserves;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts;
using Transport.SharedKernel.Contracts.Driver;
using Xunit;
using Transport.Domain.Vehicles;

namespace Transport.Tests.DriverBusinessTests;

public class DriverBusinessTests : TestBase
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

    [Fact]
    public async Task Delete_ShouldFail_WhenDriverNotFound()
    {
        // Arrange
        var drivers = new List<Driver>();
        _contextMock.Setup(x => x.Drivers).Returns(GetQueryableMockDbSet(drivers).Object);

        // Act
        var result = await _driverBusiness.Delete(99);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DriverError.DriverNotFound);
    }

    [Fact]
    public async Task Delete_ShouldSetStatusToDeleted_WhenDriverExists()
    {
        // Arrange
        var driver = new Driver { DriverId = 1, Status = EntityStatusEnum.Active };
        var drivers = new List<Driver> { driver };

        _contextMock.Setup(x => x.Drivers).Returns(GetQueryableMockDbSet(drivers).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Act
        var result = await _driverBusiness.Delete(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        driver.Status.Should().Be(EntityStatusEnum.Deleted);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenDriverNotFound()
    {
        var drivers = new List<Driver>();
        _contextMock.Setup(x => x.Drivers).Returns(GetQueryableMockDbSet(drivers).Object);

        var dto = new DriverUpdateRequestDto("NuevoNombre", "NuevoApellido");

        var result = await _driverBusiness.Update(42, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DriverError.DriverNotFound);
    }

    [Fact]
    public async Task Update_ShouldModifyDriver_WhenExists()
    {
        var driver = new Driver { DriverId = 1, FirstName = "Original", LastName = "Original" };
        var drivers = new List<Driver> { driver };
        _contextMock.Setup(x => x.Drivers).Returns(GetQueryableMockDbSet(drivers).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new DriverUpdateRequestDto("Editado", "ApellidoEditado");

        var result = await _driverBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        driver.FirstName.Should().Be("Editado");
        driver.LastName.Should().Be("ApellidoEditado");
    }

    [Fact]
    public async Task UpdateStatus_ShouldFail_WhenDriverNotFound()
    {
        var drivers = new List<Driver>();
        _contextMock.Setup(x => x.Drivers).Returns(GetQueryableMockDbSet(drivers).Object);

        var result = await _driverBusiness.UpdateStatus(100, EntityStatusEnum.Inactive);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DriverError.DriverNotFound);
    }

    [Fact]
    public async Task UpdateStatus_ShouldUpdateStatus_WhenDriverExists()
    {
        var driver = new Driver { DriverId = 1, Status = EntityStatusEnum.Active };
        var drivers = new List<Driver> { driver };

        _contextMock.Setup(x => x.Drivers).Returns(GetQueryableMockDbSet(drivers).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _driverBusiness.UpdateStatus(1, EntityStatusEnum.Inactive);

        result.IsSuccess.Should().BeTrue();
        driver.Status.Should().Be(EntityStatusEnum.Inactive);
    }

    [Fact]
    public async Task GetDriverReport_ShouldReturnFilteredPagedResult()
    {
        // Arrange
        var drivers = new List<Driver>
    {
        new Driver
        {
            DriverId = 1,
            FirstName = "Juan",
            LastName = "Pérez",
            DocumentNumber = "1234",
            Status = EntityStatusEnum.Active,
            Reserves = new List<Reserve>
            {
                new Reserve
                {
                    ReserveDate = DateTime.UtcNow,
                    Status = ReserveStatusEnum.Confirmed,
                    Vehicle = new Vehicle { InternalNumber = "V001" }
                }
            }
        },
        new Driver
        {
            DriverId = 2,
            FirstName = "Ana",
            LastName = "Gomez",
            DocumentNumber = "5678",
            Status = EntityStatusEnum.Active,
            Reserves = new List<Reserve>()
        }
    };

        _contextMock.Setup(x => x.Drivers).Returns(GetQueryableMockDbSet(drivers).Object);

        var requestDto = new PagedReportRequestDto<DriverReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new DriverReportFilterRequestDto { FirstName = "Juan" },
            SortBy = "firstname",
            SortDescending = false
        };

        // Act
        var result = await _driverBusiness.GetDriverReport(requestDto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().FirstName.Should().Be("Juan");
        result.Value.Items.First().Status.Should().Be("Active");

        result.Value.Items.First().Reserves.Should().HaveCount(1);
    }

}