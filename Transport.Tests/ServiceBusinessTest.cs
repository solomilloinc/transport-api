using FluentAssertions;
using Moq;
using Transport.Business.Data;
using Transport.Business.ServiceBusiness;
using Transport.Domain.Cities;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Service;
using Xunit;

namespace Transport.Tests.ServiceBusinessTests;

public class ServiceBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly ServiceBusiness _serviceBusiness;

    public ServiceBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _serviceBusiness = new ServiceBusiness(_contextMock.Object);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenVehicleNotFound()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync((Vehicle)null);

        var requestDto = new ServiceCreateRequestDto(
            Name: "Test",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(1),
            DepartureHour: TimeSpan.FromHours(8),
            IsHoliday: false,
            VehicleId: 999,
            StartDay: 1,
            EndDay: 5,
            Prices: new List<ReservePriceCreateRequestDto>()
        );

        var result = await _serviceBusiness.Create(requestDto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenVehicleNotAvailable()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Inactive });

        var request = new ServiceCreateRequestDto(
            Name: "Test",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(1),
            DepartureHour: TimeSpan.FromHours(8),
            IsHoliday: false,
            VehicleId: 1,
            StartDay: 1,
            EndDay: 5,
            Prices: new List<ReservePriceCreateRequestDto>()
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotAvailable);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenOriginCityNotFound()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active });

        _contextMock.Setup(x => x.Cities.FindAsync(It.IsAny<int>()))
            .ReturnsAsync((City)null);

        var request = new ServiceCreateRequestDto(
            Name: "Test",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(1),
            DepartureHour: TimeSpan.FromHours(8),
            IsHoliday: false,
            VehicleId: 1,
            StartDay: 1,
            EndDay: 5,
            Prices: new List<ReservePriceCreateRequestDto>()
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CityError.CityNotFound);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenDestinationCityNotFound()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active });

        _contextMock.SetupSequence(x => x.Cities.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new City { CityId = 1 })
            .ReturnsAsync((City)null);

        var request = new ServiceCreateRequestDto(
            Name: "Test",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(1),
            DepartureHour: TimeSpan.FromHours(8),
            IsHoliday: false,
            VehicleId: 1,
            StartDay: 1,
            EndDay: 5,
            Prices: new List<ReservePriceCreateRequestDto>()
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CityError.CityNotFound);
    }

    [Fact]
    public async Task Create_ShouldSucceed_WhenDataIsValid()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active });

        _contextMock.SetupSequence(x => x.Cities.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new City { CityId = 1 }) // Origin
            .ReturnsAsync(new City { CityId = 2 }); // Destination

        _contextMock.Setup(x => x.Services.Add(It.IsAny<Service>()))
            .Callback<Service>(s => s.ServiceId = 99);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var request = new ServiceCreateRequestDto(
            Name: "TestService",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(3),
            DepartureHour: TimeSpan.FromHours(8),
            IsHoliday: false,
            VehicleId: 1,
            StartDay: (int)DayOfWeek.Monday,
            EndDay: (int)DayOfWeek.Friday,
            Prices: new List<ReservePriceCreateRequestDto>
            {
                new(Price: 100, ReserveTypeId: (int)ReserveTypeIdEnum.Ida)
            }
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(99);
    }

    [Fact]
    public async Task GetServiceReport_ShouldReturnFilteredPagedResult()
    {
        // Arrange
        var services = new List<Service>
    {
        new Service
        {
            ServiceId = 1,
            Name = "Servicio A",
            Origin = new City { CityId = 1, Name = "Ciudad A" },
            Destination = new City { CityId = 2, Name = "Ciudad B" },
            EstimatedDuration = TimeSpan.FromHours(2),
            DepartureHour = TimeSpan.FromHours(9),
            IsHoliday = false,
            Vehicle = new Vehicle
            {
                VehicleId = 1,
                InternalNumber = "V001",
                AvailableQuantity = 5,
                VehicleType = new VehicleType
                {
                    Quantity = 20,
                    Name = "Minibus",
                    ImageBase64 = "base64string"
                }
            },
            Status = EntityStatusEnum.Active
        },
        new Service
        {
            ServiceId = 2,
            Name = "Servicio B",
            Origin = new City { CityId = 3, Name = "Ciudad C" },
            Destination = new City { CityId = 4, Name = "Ciudad D" },
            EstimatedDuration = TimeSpan.FromHours(1),
            DepartureHour = TimeSpan.FromHours(7),
            IsHoliday = true,
            Vehicle = new Vehicle
            {
                VehicleId = 2,
                InternalNumber = "V002",
                AvailableQuantity = 3,
                VehicleType = new VehicleType
                {
                    Quantity = 15,
                    Name = "Bus",
                    ImageBase64 = "base64img"
                }
            },
            Status = EntityStatusEnum.Inactive
        }
    };

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(services).Object);

        var requestDto = new PagedReportRequestDto<ServiceReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new ServiceReportFilterRequestDto("Servicio A", null, null, null, null, null),
            SortBy = "name",
            SortDescending = false
        };

        // Act
        var result = await _serviceBusiness.GetServiceReport(requestDto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Name.Should().Be("Servicio A");
        result.Value.Items.First().OriginName.Should().Be("Ciudad A");
        result.Value.Items.First().DestinationName.Should().Be("Ciudad B");
    }

    [Fact]
    public async Task Update_ShouldFail_WhenServiceNotFound()
    {
        var service = new Service
        {
            ServiceId = 2,
            ReservePrices = new List<ReservePrice>()
        };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _serviceBusiness.Update(1, new ServiceUpdateRequestDto(
            Name: "Updated",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            DepartureHour: TimeSpan.FromHours(10),
            IsHoliday: true,
            VehicleId: 1,
            StartDay: 1,
            EndDay: 5,
            Prices: new List<ServiceReservePriceDto>()
        ));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ServiceError.ServiceNotFound);
    }
    [Fact]
    public async Task Update_ShouldSucceed_WhenServiceExists()
    {
        var service = new Service
        {
            ServiceId = 1,
            ReservePrices = new List<ReservePrice>()
        };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServiceUpdateRequestDto(
            Name: "Updated",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            DepartureHour: TimeSpan.FromHours(10),
            IsHoliday: true,
            VehicleId: 1,
            StartDay: 1,
            EndDay: 5,
            Prices: new List<ServiceReservePriceDto>
            {
            new ServiceReservePriceDto(ReserveTypeId: 1, Price: 150)
            }
        );

        var result = await _serviceBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        service.Name.Should().Be(dto.Name);
        service.ReservePrices.Should().ContainSingle();
        service.ReservePrices.First().Price.Should().Be(150);
    }

    [Fact]
    public async Task Update_ShouldSucceed_WhenServiceExists_WithMultiplePrices()
    {
        var service = new Service
        {
            ServiceId = 1,
            ReservePrices = new List<ReservePrice>()
        };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServiceUpdateRequestDto(
            Name: "Updated",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            DepartureHour: TimeSpan.FromHours(10),
            IsHoliday: true,
            VehicleId: 1,
            StartDay: 1,
            EndDay: 5,
            Prices: new List<ServiceReservePriceDto>
            {
            new ServiceReservePriceDto((int)ReserveTypeIdEnum.Ida, 150),
            new ServiceReservePriceDto((int)ReserveTypeIdEnum.IdaVuelta, 250),
            new ServiceReservePriceDto((int)ReserveTypeIdEnum.Bonificado, 50)
            }
        );

        var result = await _serviceBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        service.Name.Should().Be(dto.Name);
        service.ReservePrices.Should().HaveCount(3);
        service.ReservePrices.Should().Contain(p => p.ReserveTypeId == ReserveTypeIdEnum.Ida && p.Price == 150);
        service.ReservePrices.Should().Contain(p => p.ReserveTypeId == ReserveTypeIdEnum.IdaVuelta && p.Price == 250);
        service.ReservePrices.Should().Contain(p => p.ReserveTypeId == ReserveTypeIdEnum.Bonificado && p.Price == 50);
    }

}
