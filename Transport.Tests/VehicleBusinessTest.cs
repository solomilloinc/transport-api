using FluentAssertions;
using Moq;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Vehicle;
using Transport.Business.VehicleBusiness;
using Transport.Domain.Vehicles.Abstraction;
using Transport.Business.Data;
using Xunit;
using Transport.Business.VehicleTypeBusiness;
using Transport.SharedKernel.Contracts.VehicleType;

namespace Transport.Tests.VehicleBusinessTests;

public class VehicleBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly IVehicleBusiness _vehicleBusiness;

    public VehicleBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _vehicleBusiness = new VehicleBusiness(_contextMock.Object);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenVehicleAlreadyExists()
    {
        var existing = new Vehicle { VehicleId = 1, InternalNumber = "123" };
        _contextMock.Setup(x => x.Vehicles)
            .Returns(GetQueryableMockDbSet(new List<Vehicle> { existing }).Object);

        var dto = new VehicleCreateRequestDto(1, "123");

        var result = await _vehicleBusiness.Create(dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleAlreadyExists);
    }

    [Fact]
    public async Task Create_ShouldSucceed_WhenDataIsValid()
    {
        var vehicles = new List<Vehicle>();
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity(vehicles).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new VehicleCreateRequestDto(1, "ABC");

        var result = await _vehicleBusiness.Create(dto);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Delete_ShouldFail_WhenNotFound()
    {
        _contextMock.Setup(x => x.Vehicles)
            .Returns(GetQueryableMockDbSet(new List<Vehicle>()).Object);

        var result = await _vehicleBusiness.Delete(1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task Delete_ShouldSucceed_WhenVehicleExists()
    {
        var vehicle = new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active };
        _contextMock.Setup(x => x.Vehicles)
            .Returns(GetQueryableMockDbSet(new List<Vehicle> { vehicle }).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _vehicleBusiness.Delete(1);

        result.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(EntityStatusEnum.Deleted);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenNotFound()
    {
        _contextMock.Setup(x => x.Vehicles)
            .Returns(GetQueryableMockDbSet(new List<Vehicle>()).Object);

        var dto = new VehicleUpdateRequestDto(999, "222");
        var result = await _vehicleBusiness.Update(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task Update_ShouldSucceed_WhenFound()
    {
        var vehicle = new Vehicle { VehicleId = 1, InternalNumber = "111", VehicleTypeId = 1 };
        _contextMock.Setup(x => x.Vehicles)
            .Returns(GetQueryableMockDbSet(new List<Vehicle> { vehicle }).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new VehicleUpdateRequestDto(1, "222");
        var result = await _vehicleBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        vehicle.InternalNumber.Should().Be("222");
        vehicle.VehicleTypeId.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStatus_ShouldFail_WhenNotFound()
    {
        _contextMock.Setup(x => x.Vehicles)
            .Returns(GetQueryableMockDbSet(new List<Vehicle>()).Object);

        var result = await _vehicleBusiness.UpdateStatus(9, EntityStatusEnum.Inactive);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task UpdateStatus_ShouldSucceed_WhenVehicleExists()
    {
        var vehicle = new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active };
        _contextMock.Setup(x => x.Vehicles)
            .Returns(GetQueryableMockDbSet(new List<Vehicle> { vehicle }).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _vehicleBusiness.UpdateStatus(1, EntityStatusEnum.Inactive);

        result.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(EntityStatusEnum.Inactive);
    }
}