using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Transport.Business.Data;
using Transport.Business.ServiceBusiness;
using Transport.Domain;
using Transport.Domain.Cities;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Configuration;
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

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(15);

        _serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object);
        _serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object);
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

        var result = await _serviceBusiness.Update(1, new ServiceCreateRequestDto(
            Name: "Updated",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            DepartureHour: TimeSpan.FromHours(10),
            IsHoliday: true,
            VehicleId: 1,
            StartDay: 1,
            EndDay: 5,
            Prices: new List<ReservePriceCreateRequestDto>()
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

        var dto = new ServiceCreateRequestDto(
            Name: "Updated",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            DepartureHour: TimeSpan.FromHours(10),
            IsHoliday: true,
            VehicleId: 1,
            StartDay: 1,
            EndDay: 5,
            Prices: new List<ReservePriceCreateRequestDto>()
            {
             new ReservePriceCreateRequestDto(ReserveTypeId: 1, Price: 150)
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

        var dto = new ServiceCreateRequestDto(
            Name: "Updated",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            DepartureHour: TimeSpan.FromHours(10),
            IsHoliday: true,
            VehicleId: 1,
            StartDay: 1,
            EndDay: 5,
            Prices: new List<ReservePriceCreateRequestDto>
            {
            new ReservePriceCreateRequestDto(150, (int)ReserveTypeIdEnum.Ida),
            new ReservePriceCreateRequestDto(250, (int)ReserveTypeIdEnum.IdaVuelta),
            new ReservePriceCreateRequestDto(50, (int)ReserveTypeIdEnum.Bonificado)
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

    [Fact]
    public async Task GenerateFutureReserves_ShouldCreateExpectedReserves()
    {
        // Arrange
        var reserveGenerationDays = 3;
        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(reserveGenerationDays);

        var today = DateTime.Today;
        var dayOfWeek = today.DayOfWeek;

        var vehicle = new Vehicle
        {
            VehicleId = 1,
            InternalNumber = "ABC123",
            Status = EntityStatusEnum.Active,
            AvailableQuantity = 10,
            VehicleType = new VehicleType { Name = "Bus", Quantity = 50 }
        };

        var service = new Service
        {
            ServiceId = 1,
            Name = "Mocked Service",
            StartDay = dayOfWeek,
            EndDay = dayOfWeek,
            DepartureHour = TimeSpan.FromHours(8),
            EstimatedDuration = TimeSpan.FromHours(2),
            IsHoliday = false,
            Vehicle = vehicle,
            VehicleId = vehicle.VehicleId,
            OriginId = 1,
            DestinationId = 2,
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice
            {
                ReservePriceId = 1,
                Price = 100,
                ReserveTypeId = ReserveTypeIdEnum.Ida
            }
        }
        };

        var services = new List<Service> { service };
        var vehicles = new List<Vehicle> { vehicle };
        var reserves = new List<Reserve>();
        var prices = service.ReservePrices;

        var holidays = new List<Holiday>();

        // Mock DbSets
        ContextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services).Object);
        ContextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity(vehicles).Object);
        ContextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves).Object);
        ContextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays).Object);

        SetupSaveChangesWithOutboxAsync(ContextMock);

        var serviceBusiness = new ServiceBusiness(ContextMock.Object, reserveOptionMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.NotEmpty(reserves);
        Assert.All(reserves, r =>
        {
            Assert.Equal(vehicle.VehicleId, r.VehicleId);
            Assert.Equal(ReserveStatusEnum.Available, r.Status);
            Assert.InRange(r.ReserveDate.Date, today, today.AddDays(reserveGenerationDays));
        });
    }

    [Fact]
    public async Task GenerateFutureReserves_ShouldNotCreateReserves_WhenAllNextDaysAreOutsideServiceDays()
    {
        // Arrange
        var reserveGenerationDays = 3;
        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(reserveGenerationDays);

        var today = DateTime.Today;
        var todayDayOfWeek = (int)today.DayOfWeek;

        // Forzamos un rango que excluya todos los próximos días
        // Por ejemplo: solo opera los domingos (0), y estamos en lunes
        var startDay = DayOfWeek.Sunday;
        var endDay = DayOfWeek.Sunday;

        var vehicle = new Vehicle
        {
            VehicleId = 1,
            InternalNumber = "ABC123",
            Status = EntityStatusEnum.Active,
            AvailableQuantity = 10,
            VehicleType = new VehicleType { Name = "Bus", Quantity = 50 }
        };

        var service = new Service
        {
            ServiceId = 1,
            Name = "No aplica días",
            StartDay = startDay,
            EndDay = endDay,
            DepartureHour = TimeSpan.FromHours(8),
            EstimatedDuration = TimeSpan.FromHours(2),
            IsHoliday = false,
            VehicleId = 1,
            OriginId = 1,
            DestinationId = 2,
            Vehicle = vehicle,
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { Price = 100, ReserveTypeId = ReserveTypeIdEnum.Ida }
        }
        };

        var services = new List<Service> { service };
        var reserves = new List<Reserve>();

        ContextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services).Object);
        ContextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity([vehicle]).Object);
        ContextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves).Object);
        ContextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(new List<Holiday>()).Object);

        SetupSaveChangesWithOutboxAsync(ContextMock);

        var serviceBusiness = new ServiceBusiness(ContextMock.Object, reserveOptionMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.Empty(reserves);
    }


    [Fact]
    public async Task GenerateFutureReserves_ShouldSkipHolidays_WhenIsHolidayIsFalse()
    {
        // Arrange
        var reserveGenerationDays = 3;
        var today = DateTime.Today;
        var feriado = today.AddDays(1); // Mañana es feriado

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(reserveGenerationDays);

        var vehicle = new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active };
        var service = new Service
        {
            ServiceId = 1,
            Name = "No feriado",
            StartDay = DayOfWeek.Sunday,
            EndDay = DayOfWeek.Saturday,
            DepartureHour = TimeSpan.FromHours(8),
            EstimatedDuration = TimeSpan.FromHours(2),
            IsHoliday = false,
            Vehicle = vehicle,
            VehicleId = vehicle.VehicleId,
            OriginId = 1,
            DestinationId = 2,
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { Price = 100, ReserveTypeId = ReserveTypeIdEnum.Ida }
        }
        };

        var holidays = new List<Holiday>
    {
        new Holiday { HolidayDate = feriado, Description = "Feriado test" }
    };

        var services = new List<Service> { service };
        var reserves = new List<Reserve>();

        ContextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services).Object);
        ContextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity([vehicle]).Object);
        ContextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves).Object);
        ContextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays).Object);

        SetupSaveChangesWithOutboxAsync(ContextMock);

        var serviceBusiness = new ServiceBusiness(ContextMock.Object, reserveOptionMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.DoesNotContain(reserves, r => r.ReserveDate.Date == feriado.Date);
    }

    [Fact]
    public async Task GenerateFutureReserves_ShouldCreateReservesOnHolidays_WhenIsHolidayIsTrue()
    {
        // Arrange
        var reserveGenerationDays = 3;
        var today = DateTime.Today;
        var feriado = today.AddDays(2);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(reserveGenerationDays);

        var vehicle = new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active };
        var service = new Service
        {
            ServiceId = 1,
            Name = "Servicio feriado",
            StartDay = DayOfWeek.Sunday,
            EndDay = DayOfWeek.Saturday,
            DepartureHour = TimeSpan.FromHours(8),
            EstimatedDuration = TimeSpan.FromHours(2),
            IsHoliday = true,
            Vehicle = vehicle,
            VehicleId = vehicle.VehicleId,
            OriginId = 1,
            DestinationId = 2,
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { Price = 100, ReserveTypeId = ReserveTypeIdEnum.Ida }
        }
        };

        var holidays = new List<Holiday>
    {
        new Holiday { HolidayDate = feriado, Description = "Feriado test" }
    };

        var services = new List<Service> { service };
        var reserves = new List<Reserve>();

        ContextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services).Object);
        ContextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity([vehicle]).Object);
        ContextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves).Object);
        ContextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays).Object);

        SetupSaveChangesWithOutboxAsync(ContextMock);

        var serviceBusiness = new ServiceBusiness(ContextMock.Object, reserveOptionMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.Contains(reserves, r => r.ReserveDate.Date == feriado.Date);
    }

    [Fact]
    public async Task GenerateFutureReserves_ShouldNotCreateReserves_WhenServiceHasNoPrices()
    {
        // Arrange
        var reserveGenerationDays = 3;
        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(reserveGenerationDays);

        var today = DateTime.Today;
        var dayOfWeek = today.DayOfWeek;

        var vehicle = new Vehicle
        {
            VehicleId = 1,
            InternalNumber = "ABC123",
            Status = EntityStatusEnum.Active,
            AvailableQuantity = 10,
            VehicleType = new VehicleType { Name = "Bus", Quantity = 50 }
        };

        var service = new Service
        {
            ServiceId = 1,
            Name = "Servicio sin precios",
            StartDay = dayOfWeek,
            EndDay = dayOfWeek,
            DepartureHour = TimeSpan.FromHours(8),
            EstimatedDuration = TimeSpan.FromHours(2),
            IsHoliday = false,
            Vehicle = vehicle,
            VehicleId = vehicle.VehicleId,
            OriginId = 1,
            DestinationId = 2,
            ReservePrices = new List<ReservePrice>() // <--- Sin precios
        };

        var services = new List<Service> { service };
        var vehicles = new List<Vehicle> { vehicle };
        var reserves = new List<Reserve>();
        var holidays = new List<Holiday>();

        ContextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services).Object);
        ContextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity(vehicles).Object);
        ContextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves).Object);
        ContextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays).Object);

        SetupSaveChangesWithOutboxAsync(ContextMock);

        var serviceBusiness = new ServiceBusiness(ContextMock.Object, reserveOptionMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.Empty(reserves);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenStartDayIsGreaterThanEndDay()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active });

        _contextMock.SetupSequence(x => x.Cities.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new City { CityId = 1 }) // Origin
            .ReturnsAsync(new City { CityId = 2 }); // Destination

        var request = new ServiceCreateRequestDto(
            Name: "InvalidDayRange",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            DepartureHour: TimeSpan.FromHours(9),
            IsHoliday: false,
            VehicleId: 1,
            StartDay: 5,
            EndDay: 2, 
            Prices: new List<ReservePriceCreateRequestDto>
            {
            new(Price: 120, ReserveTypeId: (int)ReserveTypeIdEnum.Ida)
            }
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ServiceError.InvalidDayRange);
    }

    [Fact]
    public async Task UpdatePricesByPercentageAsync_ShouldUpdatePrices_WhenValidRequestDto()
    {
        // Arrange
        var reserveOptionMock = new Mock<IReserveOption>();

        var requestDto = new PriceMassiveUpdateRequestDto(
            new List<PriceUpdateDto>
            {
            new PriceUpdateDto((int)ReserveTypeIdEnum.Ida, 10),
            new PriceUpdateDto((int)ReserveTypeIdEnum.IdaVuelta, 20)
            });

        var service = new Service
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
                InternalNumber = "ABC123",
                Status = EntityStatusEnum.Active,
                AvailableQuantity = 10,
                VehicleType = new VehicleType { Name = "Bus", Quantity = 50 }
            },
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Status = EntityStatusEnum.Active, Price = 100m },
            new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Status = EntityStatusEnum.Active, Price = 200m },
            new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Bonificado, Status = EntityStatusEnum.Active, Price = 90m }
        }
        };

        var services = new List<Service> { service };

        ContextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services).Object);

        SetupSaveChangesWithOutboxAsync(ContextMock);

        // Act
        var serviceBusiness = new ServiceBusiness(ContextMock.Object, reserveOptionMock.Object);

        var result = await serviceBusiness.UpdatePricesByPercentageAsync(requestDto);

        var requestServiceReportDto = new PagedReportRequestDto<ServiceReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new ServiceReportFilterRequestDto("Servicio A", null, null, null, null, null),
            SortBy = "name",
            SortDescending = false
        };

        var serviceReportResult = await serviceBusiness.GetServiceReport(requestServiceReportDto);

        // Assert
        Assert.True(serviceReportResult.IsSuccess);

        // Verificar que los precios actualizados estén en el reporte
        var reportService = serviceReportResult.Value.Items.FirstOrDefault();
        Assert.NotNull(reportService);

        var idaPriceReport = reportService.ReservePrices.FirstOrDefault(rp => rp.ReserveTypeId == (int)ReserveTypeIdEnum.Ida);
        var idaVueltaPriceReport = reportService.ReservePrices.FirstOrDefault(rp => rp.ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta);

        Assert.NotNull(idaPriceReport);
        Assert.Equal(110m, idaPriceReport.Price);  // 100 + 10%

        Assert.NotNull(idaVueltaPriceReport);
        Assert.Equal(240m, idaVueltaPriceReport.Price);  // 200 + 20%
        Assert.True(result.IsSuccess);
        Assert.Equal(110m, service.ReservePrices.First(p => p.ReserveTypeId == ReserveTypeIdEnum.Ida).Price);  // 100 + 10%
        Assert.Equal(240m, service.ReservePrices.First(p => p.ReserveTypeId == ReserveTypeIdEnum.IdaVuelta).Price);  // 200 + 20%
    }

}
