using FluentAssertions;
using Moq;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.VehicleType;
using Transport.Business.VehicleTypeBusiness;
using Transport.Domain.Vehicles.Abstraction;
using Transport.Business.Data;
using Xunit;
using Transport.SharedKernel.Contracts.Vehicle;

namespace Transport.Tests.VehicleTypeBusinessTests;

public class VehicleTypeBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly IVehicleTypeBusiness _vehicleTypeBusiness;

    public VehicleTypeBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _vehicleTypeBusiness = new VehicleTypeBusiness(_contextMock.Object);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenVehicleTypeAlreadyExists()
    {
        var existing = new VehicleType { VehicleTypeId = 1, Name = "Truck" };
        _contextMock.Setup(x => x.VehicleTypes)
            .Returns(GetQueryableMockDbSet(new List<VehicleType> { existing }).Object);

        var dto = new VehicleTypeCreateRequestDto("Truck", null, 10, null);

        var result = await _vehicleTypeBusiness.Create(dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleAlreadyExists);
    }

    [Fact]
    public async Task Create_ShouldSucceed_WhenDataIsValid()
    {
        var vehicleTypes = new List<VehicleType>();
        _contextMock.Setup(x => x.VehicleTypes).Returns(GetMockDbSetWithIdentity(vehicleTypes).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new VehicleTypeCreateRequestDto("SUV", null, 10, null);

        var result = await _vehicleTypeBusiness.Create(dto);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);
    }



    [Fact]
    public async Task Create_ShouldSucceed_WhenDataIsValid_WithVehiclesList()
    {
        var vehicleTypes = new List<VehicleType>();
        var vehiclesInDb = new List<Vehicle>
       {
           new Vehicle { InternalNumber = "123", VehicleTypeId = 0 },
           new Vehicle { InternalNumber = "456", VehicleTypeId = 0 }
       };

        _contextMock.Setup(x => x.VehicleTypes).Returns(GetMockDbSetWithIdentity(vehicleTypes).Object);
        _contextMock.Setup(x => x.Vehicles).Returns(GetQueryableMockDbSet(vehiclesInDb).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var vehicles = new List<VehicleCreateRequestDto>
       {
           new VehicleCreateRequestDto(0, "123", null),
           new VehicleCreateRequestDto(0, "456", null)
       };

        var dto = new VehicleTypeCreateRequestDto("SUV", null, 10, vehicles);

        var result = await _vehicleTypeBusiness.Create(dto);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);

        var createdVehicleType = vehicleTypes[0];
        createdVehicleType.Vehicles.Should().HaveCount(2);
        createdVehicleType.Vehicles.Should().Contain(v => v.InternalNumber == "123");
        createdVehicleType.Vehicles.Should().Contain(v => v.InternalNumber == "456");

        _contextMock.Verify(x => x.Vehicles.Add(It.IsAny<Vehicle>()), Times.Never);
    }


    [Fact]
    public async Task Delete_ShouldFail_WhenNotFound()
    {
        _contextMock.Setup(x => x.VehicleTypes)
            .Returns(GetQueryableMockDbSet(new List<VehicleType>()).Object);

        var result = await _vehicleTypeBusiness.Delete(1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task Delete_ShouldSucceed_WhenVehicleTypeExists()
    {
        var vehicleType = new VehicleType { VehicleTypeId = 1, Status = EntityStatusEnum.Active };
        _contextMock.Setup(x => x.VehicleTypes)
            .Returns(GetQueryableMockDbSet(new List<VehicleType> { vehicleType }).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _vehicleTypeBusiness.Delete(1);

        result.IsSuccess.Should().BeTrue();
        vehicleType.Status.Should().Be(EntityStatusEnum.Deleted);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenNotFound()
    {
        _contextMock.Setup(x => x.VehicleTypes)
            .Returns(GetQueryableMockDbSet(new List<VehicleType>()).Object);

        var dto = new VehicleTypeUpdateRequestDto("New Name", null, 1);
        var result = await _vehicleTypeBusiness.Update(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task Update_ShouldSucceed_WhenFound()
    {
        var vehicleType = new VehicleType { VehicleTypeId = 1, Name = "Car" };
        _contextMock.Setup(x => x.VehicleTypes)
            .Returns(GetQueryableMockDbSet(new List<VehicleType> { vehicleType }).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new VehicleTypeUpdateRequestDto("Truck", null, 1);
        var result = await _vehicleTypeBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        vehicleType.Name.Should().Be("Truck");
    }

    [Fact]
    public async Task UpdateStatus_ShouldFail_WhenNotFound()
    {
        _contextMock.Setup(x => x.VehicleTypes)
            .Returns(GetQueryableMockDbSet(new List<VehicleType>()).Object);

        var result = await _vehicleTypeBusiness.UpdateStatus(9, EntityStatusEnum.Inactive);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task UpdateStatus_ShouldSucceed_WhenVehicleTypeExists()
    {
        var vehicleType = new VehicleType { VehicleTypeId = 1, Status = EntityStatusEnum.Active };
        _contextMock.Setup(x => x.VehicleTypes)
            .Returns(GetQueryableMockDbSet(new List<VehicleType> { vehicleType }).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _vehicleTypeBusiness.UpdateStatus(1, EntityStatusEnum.Inactive);

        result.IsSuccess.Should().BeTrue();
        vehicleType.Status.Should().Be(EntityStatusEnum.Inactive);
    }
}
