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
using Transport.Domain.Trips;
using Xunit;

namespace Transport.Tests.ServiceBusinessTests;

public class ServiceBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly ServiceBusiness _serviceBusiness;

    public ServiceBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(15);

        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(new DateTime(2025, 05, 12));

        _serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenTripNotFound()
    {
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip>()));

        var schedules = new List<ServiceScheduleCreateDto>
        {
            new(0, false, TimeSpan.FromHours(8))
        };

        var requestDto = new ServiceCreateRequestDto(
            Name: "Test",
            TripId: 999,
            EstimatedDuration: TimeSpan.FromHours(1),
            VehicleId: 1,
            StartDay: DayOfWeek.Monday,
            EndDay: DayOfWeek.Friday,
            Schedules: schedules
        );

        var result = await _serviceBusiness.Create(requestDto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(TripError.TripNotFound);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenVehicleNotFound()
    {
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { new Trip { TripId = 1, Status = EntityStatusEnum.Active } }));

        _contextMock.Setup(x => x.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle>()));

        var schedules = new List<ServiceScheduleCreateDto>
        {
            new(0, false, TimeSpan.FromHours(8))
        };

        var requestDto = new ServiceCreateRequestDto(
            Name: "Test",
            TripId: 1,
            EstimatedDuration: TimeSpan.FromHours(1),
            VehicleId: 999,
            StartDay: DayOfWeek.Monday,
            EndDay: DayOfWeek.Friday,
            Schedules: schedules
        );

        var result = await _serviceBusiness.Create(requestDto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenVehicleNotAvailable()
    {
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { new Trip { TripId = 1, Status = EntityStatusEnum.Active } }));

        _contextMock.Setup(x => x.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle> { new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Inactive } }));

        var schedules = new List<ServiceScheduleCreateDto>
        {
            new(0, false, TimeSpan.FromHours(8))
        };

        var request = new ServiceCreateRequestDto(
            Name: "Test",
            TripId: 1,
            EstimatedDuration: TimeSpan.FromHours(1),
            VehicleId: 1,
            StartDay: DayOfWeek.Monday,
            EndDay: DayOfWeek.Friday,
            Schedules: schedules
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotAvailable);
    }





    [Fact]
    public async Task Create_ShouldSucceed_WhenDataIsValid()
    {
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { new Trip { TripId = 1, Status = EntityStatusEnum.Active } }));

        _contextMock.Setup(x => x.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle> { new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active } }));

        var servicesList = new List<Service>();
        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(servicesList));

        _contextMock.Setup(x => x.ServiceSchedules)
            .Returns(GetQueryableMockDbSet(new List<ServiceSchedule>()));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var schedules = new List<ServiceScheduleCreateDto>
        {
            new(0, false, TimeSpan.FromHours(8))
        };

        var request = new ServiceCreateRequestDto(
            Name: "TestService",
            TripId: 1,
            EstimatedDuration: TimeSpan.FromHours(3),
            VehicleId: 1,
            StartDay: DayOfWeek.Monday,
            EndDay: DayOfWeek.Friday,
            Schedules: schedules
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeTrue();
        // Verificar que StartDay/EndDay se persistan correctamente: regresión del bug
        // en el que los servicios quedaban con el default DayOfWeek.Sunday (0,0) y
        // solo se generaban reservas los domingos.
        servicesList.Should().HaveCount(1);
        servicesList[0].StartDay.Should().Be(DayOfWeek.Monday);
        servicesList[0].EndDay.Should().Be(DayOfWeek.Friday);
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
            Trip = new Trip
            {
                OriginCity = new City { CityId = 1, Name = "Ciudad A" },
                DestinationCity = new City { CityId = 2, Name = "Ciudad B" }
            },
            EstimatedDuration = TimeSpan.FromHours(2),
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
            Trip = new Trip
            {
                OriginCity = new City { CityId = 3, Name = "Ciudad C" },
                DestinationCity = new City { CityId = 4, Name = "Ciudad D" }
            },
            EstimatedDuration = TimeSpan.FromHours(1),
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

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(services));

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
            ServiceId = 2
        };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _serviceBusiness.Update(1, new ServiceUpdateRequestDto(
            Name: "Updated",
            TripId: 1,
            EstimatedDuration: TimeSpan.FromHours(2),
            VehicleId: 1,
            StartDay: DayOfWeek.Monday,
            EndDay: DayOfWeek.Friday
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
            TripId = 1, // mismo TripId que el DTO — no dispara revalidación del Trip
            // Valores iniciales incorrectos (default silencioso) que el Update debe sobrescribir.
            StartDay = DayOfWeek.Sunday,
            EndDay = DayOfWeek.Sunday,
        };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }));

        _contextMock.Setup(x => x.ServiceDirections)
            .Returns(GetQueryableMockDbSet(new List<ServiceDirection>()));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServiceUpdateRequestDto(
            Name: "Updated",
            TripId: 1,
            EstimatedDuration: TimeSpan.FromHours(2),
            VehicleId: 1,
            StartDay: DayOfWeek.Monday,
            EndDay: DayOfWeek.Friday
        );

        var result = await _serviceBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        service.Name.Should().Be(dto.Name);
        // Regresión del bug del default silencioso: el Update debe poder corregir
        // StartDay/EndDay en servicios que quedaron con el default antiguo.
        service.StartDay.Should().Be(DayOfWeek.Monday);
        service.EndDay.Should().Be(DayOfWeek.Friday);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenTripIdChangesAndTripNotFound()
    {
        var service = new Service { ServiceId = 1, TripId = 1 };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.ServiceDirections)
            .Returns(GetQueryableMockDbSet(new List<ServiceDirection>()));
        // Trip 99 no existe
        _contextMock.Setup(x => x.Trips)
            .Returns(GetQueryableMockDbSet(new List<Trip>()));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServiceUpdateRequestDto(
            Name: "X",
            TripId: 99,
            EstimatedDuration: TimeSpan.FromHours(2),
            VehicleId: 1,
            StartDay: DayOfWeek.Monday,
            EndDay: DayOfWeek.Friday
        );

        var result = await _serviceBusiness.Update(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(TripError.TripNotFound);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenTripIdChangesAndTripNotActive()
    {
        var service = new Service { ServiceId = 1, TripId = 1 };
        var inactiveTrip = new Trip { TripId = 2, Status = EntityStatusEnum.Inactive };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.ServiceDirections)
            .Returns(GetQueryableMockDbSet(new List<ServiceDirection>()));
        _contextMock.Setup(x => x.Trips)
            .Returns(GetQueryableMockDbSet(new List<Trip> { inactiveTrip }));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServiceUpdateRequestDto(
            Name: "X",
            TripId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            VehicleId: 1,
            StartDay: DayOfWeek.Monday,
            EndDay: DayOfWeek.Friday
        );

        var result = await _serviceBusiness.Update(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(TripError.TripNotActive);
    }

    [Fact]
    public async Task Update_ShouldSucceed_WhenTripIdChangesToActiveTrip()
    {
        var service = new Service { ServiceId = 1, TripId = 1 };
        var newTrip = new Trip { TripId = 2, Status = EntityStatusEnum.Active };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.ServiceDirections)
            .Returns(GetQueryableMockDbSet(new List<ServiceDirection>()));
        _contextMock.Setup(x => x.Trips)
            .Returns(GetQueryableMockDbSet(new List<Trip> { newTrip }));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServiceUpdateRequestDto(
            Name: "X",
            TripId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            VehicleId: 1,
            StartDay: DayOfWeek.Monday,
            EndDay: DayOfWeek.Friday
        );

        var result = await _serviceBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        service.TripId.Should().Be(2);
    }


    [Fact]
    public async Task GenerateFutureReserves_ShouldCreateExpectedReserves_WithServiceSchedules()
    {
        // Arrange
        var today = _dateTimeProviderMock.Object.UtcNow;
        var dayOfWeek = (int)today.DayOfWeek;

        var vehicle = new Vehicle
        {
            VehicleId = 1,
            InternalNumber = "ABC123",
            Status = EntityStatusEnum.Active,
            AvailableQuantity = 10,
            VehicleType = new VehicleType { Name = "Bus", Quantity = 50 }
        };

        var trip = new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, OriginCity = new City { CityId = 1, Name = "Origin City" }, DestinationCity = new City { CityId = 2, Name = "Destination City" } };

        var service = new Service
        {
            ServiceId = 1,
            Name = "Mocked Service",
            EstimatedDuration = TimeSpan.FromHours(2),
            Vehicle = vehicle,
            VehicleId = vehicle.VehicleId,
            TripId = trip.TripId,
            Trip = trip,

            StartDay = today.DayOfWeek,
            EndDay = today.DayOfWeek,
            Schedules = new List<ServiceSchedule>
            {
                new ServiceSchedule
                {
                    ServiceScheduleId = 1,
                    DepartureHour = TimeSpan.FromHours(8),
                    IsHoliday = false
                }
            }
        };

        var services = new List<Service> { service };
        var vehicles = new List<Vehicle> { vehicle };
        var reserves = new List<Reserve>();
        var holidays = new List<Holiday>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services));
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity(vehicles));
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves));
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { trip }));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Act
        await _serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.NotEmpty(reserves);
        Assert.All(reserves, r =>
        {
            Assert.Equal(vehicle.VehicleId, r.VehicleId);
            Assert.Equal(ReserveStatusEnum.Confirmed, r.Status);
            Assert.InRange(r.ReserveDate.Date, today.Date, today.AddDays(15).Date);
        });
    }

    [Fact]
    public async Task GenerateFutureReserves_ShouldNotCreateReserves_WhenServiceScheduleDaysDoNotMatchUpcomingDays()
    {
        // Arrange
        var today = new DateTime(2025, 05, 12); // Lunes
        var dateProviderMock = new Mock<IDateTimeProvider>();
        dateProviderMock.Setup(x => x.UtcNow).Returns(today);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(3); // Martes a Jueves

        var vehicle = new Vehicle
        {
            VehicleId = 1,
            Status = EntityStatusEnum.Active,
            AvailableQuantity = 5
        };

        var trip = new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active };

        var service = new Service
        {
            ServiceId = 1,
            EstimatedDuration = TimeSpan.FromHours(2),
            VehicleId = vehicle.VehicleId,
            Vehicle = vehicle,
            TripId = trip.TripId,
            Trip = trip,

            StartDay = DayOfWeek.Sunday,
            EndDay = DayOfWeek.Sunday,
            Schedules = new List<ServiceSchedule>
            {
                new ServiceSchedule
                {
                    ServiceScheduleId = 1,
                    DepartureHour = TimeSpan.FromHours(8),
                    IsHoliday = false
                }
            }
        };

        var services = new List<Service> { service };
        var reserves = new List<Reserve>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services));
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity([vehicle]));
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves));
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(new List<Holiday>()));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { trip }));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, dateProviderMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.Empty(reserves);
    }


    [Fact]
    public async Task GenerateFutureReserves_ShouldSkipHolidayDates_WhenServiceScheduleIsNotHoliday()
    {
        // Arrange
        var today = new DateTime(2025, 05, 12); // Lunes
        var feriado = today.AddDays(1); // Martes

        var dateProviderMock = new Mock<IDateTimeProvider>();
        dateProviderMock.Setup(x => x.UtcNow).Returns(today);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(2); // Lunes y martes

        var vehicle = new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active };
        var trip = new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, OriginCity = new City { CityId = 1, Name = "Origin City" }, DestinationCity = new City { CityId = 2, Name = "Destination City" } };

        var service = new Service
        {
            ServiceId = 1,
            EstimatedDuration = TimeSpan.FromHours(2),
            VehicleId = vehicle.VehicleId,
            Vehicle = vehicle,
            TripId = trip.TripId,
            Trip = trip,

            StartDay = DayOfWeek.Monday,
            EndDay = DayOfWeek.Tuesday,
            Schedules = new List<ServiceSchedule>
            {
                new ServiceSchedule
                {
                    ServiceScheduleId = 1,
                    DepartureHour = TimeSpan.FromHours(8),
                    IsHoliday = false // NO opera en feriados
                }
            }
        };

        var holidays = new List<Holiday>
        {
            new Holiday { HolidayDate = feriado, Description = "Feriado test" }
        };

        var reserves = new List<Reserve>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity([service]));
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity([vehicle]));
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves));
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { trip }));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, dateProviderMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.DoesNotContain(reserves, r => r.ReserveDate.Date == feriado.Date);
    }


    [Fact]
    public async Task GenerateFutureReserves_ShouldCreateReserveOnHoliday_WhenServiceScheduleAllowsHoliday()
    {
        // Arrange
        var today = new DateTime(2025, 05, 12); // Lunes
        var feriado = today.AddDays(1); // Martes

        var dateProviderMock = new Mock<IDateTimeProvider>();
        dateProviderMock.Setup(x => x.UtcNow).Returns(today);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(2);

        var vehicle = new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active };
        var trip = new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, OriginCity = new City { CityId = 1, Name = "Origin City" }, DestinationCity = new City { CityId = 2, Name = "Destination City" } };

        var service = new Service
        {
            ServiceId = 1,
            EstimatedDuration = TimeSpan.FromHours(2),
            VehicleId = vehicle.VehicleId,
            Vehicle = vehicle,
            TripId = trip.TripId,
            Trip = trip,

            StartDay = DayOfWeek.Monday,
            EndDay = DayOfWeek.Friday,
            Schedules = new List<ServiceSchedule>
            {
                new ServiceSchedule
                {
                    ServiceScheduleId = 1,
                    DepartureHour = TimeSpan.FromHours(8),
                    IsHoliday = true // Sí opera en feriados
                }
            }
        };

        var holidays = new List<Holiday>
        {
            new Holiday { HolidayDate = feriado, Description = "Feriado test" }
        };

        var reserves = new List<Reserve>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity([service]));
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity([vehicle]));
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves));
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { trip }));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, dateProviderMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.Contains(reserves, r => r.ReserveDate.Date == feriado.Date);
    }

    /// <summary>
    /// Regresión del bug reportado por el usuario (servicios 1002/1003).
    /// Antes del fix, cuando un servicio se persistía con StartDay=Sunday, EndDay=Sunday
    /// (default silencioso de DayOfWeek), el generador solo creaba reservas los domingos.
    /// Con el fix, el DTO fuerza a explicitar StartDay/EndDay, y un servicio Lun-Vie debe
    /// generar una reserva por cada día hábil dentro de la ventana.
    /// </summary>
    [Fact]
    public async Task GenerateFutureReserves_ShouldCreateReservesForEachWeekday_WhenServiceRunsMondayToFriday()
    {
        // Arrange: hoy es Lunes 2025-05-12. Ventana = 7 días → L, M, X, J, V, S, D, L.
        // El servicio corre L-V, así que debe generar 6 reservas (dos lunes + M + X + J + V).
        var today = new DateTime(2025, 05, 12);
        var dateProviderMock = new Mock<IDateTimeProvider>();
        dateProviderMock.Setup(x => x.UtcNow).Returns(today);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(7);

        var vehicle = new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active };
        var trip = new Trip
        {
            TripId = 1,
            OriginCityId = 1,
            DestinationCityId = 2,
            Status = EntityStatusEnum.Active,
            OriginCity = new City { CityId = 1, Name = "Lobos" },
            DestinationCity = new City { CityId = 2, Name = "CABA" }
        };

        var service = new Service
        {
            ServiceId = 1002,
            Name = "Lobos a Caba",
            EstimatedDuration = TimeSpan.FromHours(1),
            VehicleId = vehicle.VehicleId,
            Vehicle = vehicle,
            TripId = trip.TripId,
            Trip = trip,
            StartDay = DayOfWeek.Monday,
            EndDay = DayOfWeek.Friday,
            Schedules = new List<ServiceSchedule>
            {
                new ServiceSchedule
                {
                    ServiceScheduleId = 2002,
                    DepartureHour = TimeSpan.FromHours(9),
                    IsHoliday = false,
                    Status = EntityStatusEnum.Active
                }
            }
        };

        var reserves = new List<Reserve>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity([service]));
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity([vehicle]));
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves));
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(new List<Holiday>()));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { trip }));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, dateProviderMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert: una reserva por cada día Lun-Vie dentro de la ventana [today, today+7].
        // Días hábiles esperados: 12 (L), 13 (M), 14 (X), 15 (J), 16 (V), 19 (L). Total = 6.
        reserves.Should().HaveCount(6);
        reserves.Should().OnlyContain(r =>
            r.ReserveDate.DayOfWeek != DayOfWeek.Saturday &&
            r.ReserveDate.DayOfWeek != DayOfWeek.Sunday);
        reserves.Should().OnlyContain(r => r.ServiceId == 1002);
        reserves.Should().OnlyContain(r => r.DepartureHour == TimeSpan.FromHours(9));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SyncSchedules — endpoint bulk "declarative sync"
    //
    //  Los tests cubren la matriz de operaciones del diff:
    //   - Servicio inexistente → ServiceNotFound
    //   - Schedule del payload que no pertenece al servicio → ScheduleNotInService
    //   - Crear nuevos (Id=null)
    //   - Actualizar existentes (Id con valor)
    //   - Soft-delete de los que no aparecen en el payload
    //   - No-op cuando el payload coincide exactamente con DB
    //   - Reactivación de un schedule previamente Deleted
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncSchedules_ShouldFail_WhenServiceNotFound()
    {
        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service>()));

        var result = await _serviceBusiness.SyncSchedules(1, new ServiceSchedulesSyncRequestDto(
            new List<ServiceScheduleSyncItemDto>()));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ServiceError.ServiceNotFound);
    }

    [Fact]
    public async Task SyncSchedules_ShouldFail_WhenScheduleIdDoesNotBelongToService()
    {
        var service = new Service
        {
            ServiceId = 1,
            Schedules = new List<ServiceSchedule>
            {
                new ServiceSchedule { ServiceScheduleId = 10, ServiceId = 1, DepartureHour = TimeSpan.FromHours(8), Status = EntityStatusEnum.Active }
            }
        };

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.ServiceSchedules).Returns(GetQueryableMockDbSet(service.Schedules.ToList()));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        // scheduleId = 99 no existe dentro del servicio 1
        var dto = new ServiceSchedulesSyncRequestDto(new List<ServiceScheduleSyncItemDto>
        {
            new(ServiceScheduleId: 99, DepartureHour: TimeSpan.FromHours(10), IsHoliday: false)
        });

        var result = await _serviceBusiness.SyncSchedules(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Service.ScheduleNotInService");
    }

    [Fact]
    public async Task SyncSchedules_ShouldCreateNewSchedules_WhenIdIsNull()
    {
        var service = new Service { ServiceId = 1, Schedules = new List<ServiceSchedule>() };
        var added = new List<ServiceSchedule>();

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.ServiceSchedules).Returns(GetMockDbSetWithIdentity(added));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServiceSchedulesSyncRequestDto(new List<ServiceScheduleSyncItemDto>
        {
            new(null, TimeSpan.FromHours(8),  false),
            new(null, TimeSpan.FromHours(17), false)
        });

        var result = await _serviceBusiness.SyncSchedules(1, dto);

        result.IsSuccess.Should().BeTrue();
        added.Should().HaveCount(2);
        added.Should().OnlyContain(s => s.ServiceId == 1 && s.Status == EntityStatusEnum.Active);
        added.Select(s => s.DepartureHour).Should().BeEquivalentTo(new[]
        {
            TimeSpan.FromHours(8), TimeSpan.FromHours(17)
        });
    }

    [Fact]
    public async Task SyncSchedules_ShouldUpdateExistingSchedules_WhenIdMatches()
    {
        var existing = new ServiceSchedule
        {
            ServiceScheduleId = 10,
            ServiceId = 1,
            DepartureHour = TimeSpan.FromHours(8),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };
        var service = new Service { ServiceId = 1, Schedules = new List<ServiceSchedule> { existing } };

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.ServiceSchedules).Returns(GetQueryableMockDbSet(new List<ServiceSchedule> { existing }));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServiceSchedulesSyncRequestDto(new List<ServiceScheduleSyncItemDto>
        {
            new(ServiceScheduleId: 10, DepartureHour: TimeSpan.FromHours(9), IsHoliday: true)
        });

        var result = await _serviceBusiness.SyncSchedules(1, dto);

        result.IsSuccess.Should().BeTrue();
        existing.DepartureHour.Should().Be(TimeSpan.FromHours(9));
        existing.IsHoliday.Should().BeTrue();
        existing.Status.Should().Be(EntityStatusEnum.Active); // no se toca
    }

    [Fact]
    public async Task SyncSchedules_ShouldSoftDeleteSchedulesMissingFromPayload()
    {
        var keep = new ServiceSchedule { ServiceScheduleId = 10, ServiceId = 1, DepartureHour = TimeSpan.FromHours(8), Status = EntityStatusEnum.Active };
        var remove = new ServiceSchedule { ServiceScheduleId = 11, ServiceId = 1, DepartureHour = TimeSpan.FromHours(17), Status = EntityStatusEnum.Active };
        var service = new Service { ServiceId = 1, Schedules = new List<ServiceSchedule> { keep, remove } };

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.ServiceSchedules).Returns(GetQueryableMockDbSet(new List<ServiceSchedule> { keep, remove }));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        // El payload solo trae el schedule 10 → el 11 debe terminar Deleted.
        var dto = new ServiceSchedulesSyncRequestDto(new List<ServiceScheduleSyncItemDto>
        {
            new(ServiceScheduleId: 10, DepartureHour: TimeSpan.FromHours(8), IsHoliday: false)
        });

        var result = await _serviceBusiness.SyncSchedules(1, dto);

        result.IsSuccess.Should().BeTrue();
        keep.Status.Should().Be(EntityStatusEnum.Active);
        remove.Status.Should().Be(EntityStatusEnum.Deleted);
    }

    [Fact]
    public async Task SyncSchedules_ShouldReactivateDeletedSchedule_WhenIncludedInPayload()
    {
        var zombie = new ServiceSchedule
        {
            ServiceScheduleId = 10,
            ServiceId = 1,
            DepartureHour = TimeSpan.FromHours(8),
            Status = EntityStatusEnum.Deleted // estaba borrado
        };
        var service = new Service { ServiceId = 1, Schedules = new List<ServiceSchedule> { zombie } };

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.ServiceSchedules).Returns(GetQueryableMockDbSet(new List<ServiceSchedule> { zombie }));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServiceSchedulesSyncRequestDto(new List<ServiceScheduleSyncItemDto>
        {
            new(ServiceScheduleId: 10, DepartureHour: TimeSpan.FromHours(9), IsHoliday: false)
        });

        var result = await _serviceBusiness.SyncSchedules(1, dto);

        result.IsSuccess.Should().BeTrue();
        zombie.Status.Should().Be(EntityStatusEnum.Active); // reactivado
        zombie.DepartureHour.Should().Be(TimeSpan.FromHours(9));
    }

    [Fact]
    public async Task SyncSchedules_WithEmptyPayload_ShouldSoftDeleteAllActiveSchedules()
    {
        var s1 = new ServiceSchedule { ServiceScheduleId = 10, ServiceId = 1, Status = EntityStatusEnum.Active };
        var s2 = new ServiceSchedule { ServiceScheduleId = 11, ServiceId = 1, Status = EntityStatusEnum.Active };
        var service = new Service { ServiceId = 1, Schedules = new List<ServiceSchedule> { s1, s2 } };

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.ServiceSchedules).Returns(GetQueryableMockDbSet(new List<ServiceSchedule> { s1, s2 }));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _serviceBusiness.SyncSchedules(1,
            new ServiceSchedulesSyncRequestDto(new List<ServiceScheduleSyncItemDto>()));

        result.IsSuccess.Should().BeTrue();
        s1.Status.Should().Be(EntityStatusEnum.Deleted);
        s2.Status.Should().Be(EntityStatusEnum.Deleted);
    }

}
