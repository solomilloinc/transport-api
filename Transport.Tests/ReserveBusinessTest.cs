using FluentAssertions;
using Moq;
using Transport.Business.Data;
using Transport.Business.ReserveBusiness;
using Transport.Domain.Reserves;
using Transport.Domain.Vehicles;
using Transport.Domain.Drivers;
using Transport.SharedKernel.Contracts.Reserve;
using Xunit;

namespace Transport.Tests.ReserveBusinessTests;

public class ReserveBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ReserveBusiness _reserveBusiness;

    public ReserveBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _reserveBusiness = new ReserveBusiness(_contextMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task UpdateReserveAsync_ShouldFail_WhenReserveNotFound()
    {
        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(new List<Reserve>()).Object);

        var dto = new ReserveUpdateRequestDto(1, 1, DateTime.UtcNow, TimeSpan.FromHours(9), 1);
        var result = await _reserveBusiness.UpdateReserveAsync(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveError.NotFound);
    }

    [Fact]
    public async Task UpdateReserveAsync_ShouldFail_WhenVehicleNotFound()
    {
        var reserve = new Reserve { ReserveId = 1 };
        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(new List<Reserve> { reserve }).Object);

        _contextMock.Setup(x => x.Vehicles.FindAsync(1))
            .ReturnsAsync((Vehicle)null); // vehículo no encontrado

        var dto = new ReserveUpdateRequestDto(1, null, null, null, null);
        var result = await _reserveBusiness.UpdateReserveAsync(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task UpdateReserveAsync_ShouldFail_WhenDriverNotFound()
    {
        var reserve = new Reserve { ReserveId = 1 };
        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(new List<Reserve> { reserve }).Object);

        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new Vehicle { VehicleId = 1 }); // simular que sí existe el vehículo

        _contextMock.Setup(x => x.Drivers.FindAsync(1))
            .ReturnsAsync((Driver)null); // chofer no encontrado

        var dto = new ReserveUpdateRequestDto(null, 1, null, null, null);
        var result = await _reserveBusiness.UpdateReserveAsync(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DriverError.DriverNotFound);
    }

    [Fact]
    public async Task UpdateReserveAsync_ShouldSucceed_WhenDataIsValid()
    {
        var reserve = new Reserve { ReserveId = 1, Status = ReserveStatusEnum.Available };
        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(new List<Reserve> { reserve }).Object);

        _contextMock.Setup(x => x.Vehicles.FindAsync(2))
            .ReturnsAsync(new Vehicle { VehicleId = 2 });

        _contextMock.Setup(x => x.Drivers.FindAsync(3))
            .ReturnsAsync(new Driver { DriverId = 3 });

        _contextMock.Setup(x => x.SaveChangesWithOutboxAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dto = new ReserveUpdateRequestDto(2, 3, DateTime.Today, TimeSpan.FromHours(10), (int)ReserveStatusEnum.Confirmed);
        var result = await _reserveBusiness.UpdateReserveAsync(1, dto);

        result.IsSuccess.Should().BeTrue();
        reserve.VehicleId.Should().Be(2);
        reserve.DriverId.Should().Be(3);
        reserve.ReserveDate.Should().Be(DateTime.Today);
        reserve.DepartureHour.Should().Be(TimeSpan.FromHours(10));
        reserve.Status.Should().Be(ReserveStatusEnum.Confirmed);
    }
}
