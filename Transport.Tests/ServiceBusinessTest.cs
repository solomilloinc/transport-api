using FluentAssertions;
using Moq;
using Transport.Business.Data;
using Transport.Business.ServiceBusiness;
using Transport.Domain;
using Transport.Domain.Cities;
using Transport.Domain.FrequentSubscriptions;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Tenants;
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
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(new DateTime(2025, 05, 12)); // Monday

        // TenantConfigs vacío por default → GetReserveGenerationDaysAsync cae al IReserveOption (15).
        _contextMock.Setup(c => c.TenantConfigs).Returns(GetQueryableMockDbSet(new List<TenantConfig>()));

        _serviceBusiness = new ServiceBusiness(
            _contextMock.Object,
            reserveOptionMock.Object,
            _dateTimeProviderMock.Object,
            new FakeTenantContext { TenantId = 1 });
    }

    private static ServiceCreateRequestDto BuildRequest(
        int tripId = 1,
        int vehicleId = 1,
        DayOfWeek dayOfWeek = DayOfWeek.Monday,
        TimeSpan? departureHour = null,
        string name = "Test") =>
        new(
            Name: name,
            TripId: tripId,
            VehicleId: vehicleId,
            DayOfWeek: dayOfWeek,
            DepartureHour: departureHour ?? TimeSpan.FromHours(8),
            EstimatedDuration: TimeSpan.FromHours(2),
            IsHoliday: false);

    [Fact]
    public async Task Create_ShouldFail_WhenTripNotFound()
    {
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip>()));

        var result = await _serviceBusiness.Create(BuildRequest(tripId: 999));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(TripError.TripNotFound);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenVehicleNotFound()
    {
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { new() { TripId = 1, Status = EntityStatusEnum.Active } }));
        _contextMock.Setup(x => x.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle>()));

        var result = await _serviceBusiness.Create(BuildRequest(vehicleId: 999));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenVehicleNotAvailable()
    {
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { new() { TripId = 1, Status = EntityStatusEnum.Active } }));
        _contextMock.Setup(x => x.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle> { new() { VehicleId = 1, Status = EntityStatusEnum.Inactive } }));

        var result = await _serviceBusiness.Create(BuildRequest());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotAvailable);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenSlotConflict_WithActiveService()
    {
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { new() { TripId = 1, Status = EntityStatusEnum.Active } }));
        _contextMock.Setup(x => x.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle> { new() { VehicleId = 1, Status = EntityStatusEnum.Active } }));

        var existing = new Service
        {
            ServiceId = 5,
            TripId = 1,
            DayOfWeek = DayOfWeek.Monday,
            DepartureHour = TimeSpan.FromHours(8),
            Status = EntityStatusEnum.Active
        };
        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { existing }));

        var result = await _serviceBusiness.Create(BuildRequest(
            tripId: 1,
            dayOfWeek: DayOfWeek.Monday,
            departureHour: TimeSpan.FromHours(8)));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Service.SlotConflict");
    }

    [Fact]
    public async Task Create_ShouldSucceed_WhenDataIsValid()
    {
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { new() { TripId = 1, Status = EntityStatusEnum.Active } }));
        _contextMock.Setup(x => x.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle> { new() { VehicleId = 1, Status = EntityStatusEnum.Active } }));

        var servicesList = new List<Service>();
        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(servicesList));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _serviceBusiness.Create(BuildRequest());

        result.IsSuccess.Should().BeTrue();
        servicesList.Should().HaveCount(1);
        servicesList[0].DayOfWeek.Should().Be(DayOfWeek.Monday);
        servicesList[0].DepartureHour.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public async Task GetServiceReport_ShouldReturnFilteredPagedResult()
    {
        var services = new List<Service>
        {
            new()
            {
                ServiceId = 1,
                Name = "Servicio A",
                DayOfWeek = DayOfWeek.Monday,
                DepartureHour = TimeSpan.FromHours(8),
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
            new()
            {
                ServiceId = 2,
                Name = "Servicio B",
                DayOfWeek = DayOfWeek.Tuesday,
                DepartureHour = TimeSpan.FromHours(12),
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

        var result = await _serviceBusiness.GetServiceReport(requestDto);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Name.Should().Be("Servicio A");
        result.Value.Items.First().DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.Value.Items.First().DepartureHour.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public async Task Update_ShouldFail_WhenServiceNotFound()
    {
        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { new() { ServiceId = 2 } }));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var result = await _serviceBusiness.Update(1, BuildRequest());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ServiceError.ServiceNotFound);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenSlotConflict_WithDifferentActiveService()
    {
        var target = new Service { ServiceId = 1, TripId = 1, DayOfWeek = DayOfWeek.Monday, DepartureHour = TimeSpan.FromHours(8), Status = EntityStatusEnum.Active };
        var other = new Service { ServiceId = 2, TripId = 1, DayOfWeek = DayOfWeek.Tuesday, DepartureHour = TimeSpan.FromHours(12), Status = EntityStatusEnum.Active };

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { target, other }));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { new() { TripId = 1, Status = EntityStatusEnum.Active } }));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = BuildRequest(tripId: 1, dayOfWeek: DayOfWeek.Tuesday, departureHour: TimeSpan.FromHours(12));

        var result = await _serviceBusiness.Update(target.ServiceId, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Service.SlotConflict");
    }

    [Fact]
    public async Task Update_ShouldSucceed_WhenServiceExists()
    {
        var service = new Service { ServiceId = 1, TripId = 1, VehicleId = 1, DayOfWeek = DayOfWeek.Sunday, DepartureHour = TimeSpan.FromHours(20), Status = EntityStatusEnum.Active };
        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { new() { TripId = 1, Status = EntityStatusEnum.Active } }));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = BuildRequest(name: "Updated", dayOfWeek: DayOfWeek.Wednesday, departureHour: TimeSpan.FromHours(9));

        var result = await _serviceBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        service.Name.Should().Be("Updated");
        service.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
        service.DepartureHour.Should().Be(TimeSpan.FromHours(9));
    }

    [Fact]
    public async Task Update_ShouldFail_WhenVehicleCapacityBelowSubscriptions()
    {
        var service = new Service { ServiceId = 1, TripId = 1, VehicleId = 5, DayOfWeek = DayOfWeek.Monday, DepartureHour = TimeSpan.FromHours(8), Status = EntityStatusEnum.Active };
        var smallVehicle = new Vehicle { VehicleId = 7, Status = EntityStatusEnum.Active, AvailableQuantity = 1 };
        var subs = new List<FrequentSubscription>
        {
            new() { FrequentSubscriptionId = 1, OutboundServiceId = 1, CustomerId = 1, Status = EntityStatusEnum.Active, ReserveTypeId = ReserveTypeIdEnum.Ida },
            new() { FrequentSubscriptionId = 2, OutboundServiceId = 1, CustomerId = 2, Status = EntityStatusEnum.Active, ReserveTypeId = ReserveTypeIdEnum.Ida },
        };

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { new() { TripId = 1, Status = EntityStatusEnum.Active } }));
        _contextMock.Setup(x => x.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle> { smallVehicle }));
        _contextMock.Setup(x => x.FrequentSubscriptions).Returns(GetQueryableMockDbSet(subs));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServiceCreateRequestDto(
            Name: "x", TripId: 1, VehicleId: 7,
            DayOfWeek: DayOfWeek.Monday, DepartureHour: TimeSpan.FromHours(8),
            EstimatedDuration: TimeSpan.FromHours(2), IsHoliday: false);

        var result = await _serviceBusiness.Update(1, dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Service.VehicleCapacityBelowSubscriptions");
    }

    [Fact]
    public async Task Delete_ShouldFail_WhenHasActiveSubscriptions()
    {
        var service = new Service { ServiceId = 1, Status = EntityStatusEnum.Active };
        var sub = new FrequentSubscription { FrequentSubscriptionId = 1, OutboundServiceId = 1, CustomerId = 1, ReserveTypeId = ReserveTypeIdEnum.Ida, Status = EntityStatusEnum.Active };

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _contextMock.Setup(x => x.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { sub }));

        var result = await _serviceBusiness.Delete(1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Service.HasActiveSubscriptions");
        service.Status.Should().Be(EntityStatusEnum.Active);
    }

    [Fact]
    public async Task GenerateFutureReserves_ShouldCreateOnePerMatchingWeekday()
    {
        var today = _dateTimeProviderMock.Object.UtcNow; // Monday
        var trip = new Trip { TripId = 1, Status = EntityStatusEnum.Active, OriginCity = new City { Name = "Origin" }, DestinationCity = new City { Name = "Destination" } };
        var service = new Service
        {
            ServiceId = 1,
            Name = "Mon 08:00",
            TripId = 1,
            Trip = trip,
            VehicleId = 1,
            DayOfWeek = today.DayOfWeek,
            DepartureHour = TimeSpan.FromHours(8),
            EstimatedDuration = TimeSpan.FromHours(2),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };

        var reserves = new List<Reserve>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }));
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves));
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(new List<Holiday>()));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        await _serviceBusiness.GenerateFutureReservesAsync();

        // Today + 15 days = 3 Mondays in the window (today, +7, +14)
        reserves.Should().HaveCount(3);
        reserves.Should().AllSatisfy(r =>
        {
            r.VehicleId.Should().Be(1);
            r.ServiceId.Should().Be(1);
            r.Status.Should().Be(ReserveStatusEnum.Confirmed);
            r.DepartureHour.Should().Be(TimeSpan.FromHours(8));
            r.ReserveDate.DayOfWeek.Should().Be(today.DayOfWeek);
        });
    }

    [Fact]
    public async Task GenerateFutureReserves_ShouldNotCreateReserves_WhenServiceDayOfWeekDoesNotMatch()
    {
        var today = new DateTime(2025, 05, 12); // Monday
        var dateProviderMock = new Mock<IDateTimeProvider>();
        dateProviderMock.Setup(x => x.UtcNow).Returns(today);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(3); // Tuesday-Thursday

        var trip = new Trip { TripId = 1, Status = EntityStatusEnum.Active, OriginCity = new City { Name = "Origin" }, DestinationCity = new City { Name = "Destination" } };
        var service = new Service
        {
            ServiceId = 1,
            Name = "Sun 08:00",
            TripId = 1,
            Trip = trip,
            VehicleId = 1,
            DayOfWeek = DayOfWeek.Sunday, // never in window
            DepartureHour = TimeSpan.FromHours(8),
            EstimatedDuration = TimeSpan.FromHours(2),
            Status = EntityStatusEnum.Active
        };

        var reserves = new List<Reserve>();
        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }));
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves));
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(new List<Holiday>()));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, dateProviderMock.Object, new FakeTenantContext { TenantId = 1 });

        await serviceBusiness.GenerateFutureReservesAsync();

        reserves.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateFutureReserves_ShouldSkipHolidayDates_WhenServiceIsNotHoliday()
    {
        var today = new DateTime(2025, 05, 12); // Monday
        var feriado = today.AddDays(7); // Following Monday
        var dateProviderMock = new Mock<IDateTimeProvider>();
        dateProviderMock.Setup(x => x.UtcNow).Returns(today);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(10);

        var trip = new Trip { TripId = 1, Status = EntityStatusEnum.Active, OriginCity = new City { Name = "Origin" }, DestinationCity = new City { Name = "Destination" } };
        var service = new Service
        {
            ServiceId = 1,
            Name = "Mon 08:00",
            TripId = 1,
            Trip = trip,
            VehicleId = 1,
            DayOfWeek = DayOfWeek.Monday,
            DepartureHour = TimeSpan.FromHours(8),
            EstimatedDuration = TimeSpan.FromHours(2),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };

        var reserves = new List<Reserve>();
        var holidays = new List<Holiday> { new() { HolidayDate = feriado, Description = "Test" } };

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }));
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves));
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, dateProviderMock.Object, new FakeTenantContext { TenantId = 1 });

        await serviceBusiness.GenerateFutureReservesAsync();

        reserves.Should().NotContain(r => r.ReserveDate.Date == feriado.Date);
    }

    [Fact]
    public async Task GenerateFutureReserves_ShouldCreateReserveOnHoliday_WhenServiceAllowsHoliday()
    {
        var today = new DateTime(2025, 05, 12); // Monday
        var feriado = today.AddDays(7); // Following Monday
        var dateProviderMock = new Mock<IDateTimeProvider>();
        dateProviderMock.Setup(x => x.UtcNow).Returns(today);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(10);

        var trip = new Trip { TripId = 1, Status = EntityStatusEnum.Active, OriginCity = new City { Name = "Origin" }, DestinationCity = new City { Name = "Destination" } };
        var service = new Service
        {
            ServiceId = 1,
            Name = "Mon 08:00 holiday",
            TripId = 1,
            Trip = trip,
            VehicleId = 1,
            DayOfWeek = DayOfWeek.Monday,
            DepartureHour = TimeSpan.FromHours(8),
            EstimatedDuration = TimeSpan.FromHours(2),
            IsHoliday = true, // operates on holidays
            Status = EntityStatusEnum.Active
        };

        var reserves = new List<Reserve>();
        var holidays = new List<Holiday> { new() { HolidayDate = feriado, Description = "Test" } };

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }));
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves));
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        var serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, dateProviderMock.Object, new FakeTenantContext { TenantId = 1 });

        await serviceBusiness.GenerateFutureReservesAsync();

        reserves.Should().Contain(r => r.ReserveDate.Date == feriado.Date);
    }

    [Fact]
    public async Task GenerateFutureReserves_ShouldSkipExisting_WhenReserveAlreadyCreated()
    {
        var today = _dateTimeProviderMock.Object.UtcNow; // Monday
        var trip = new Trip { TripId = 1, Status = EntityStatusEnum.Active, OriginCity = new City { Name = "Origin" }, DestinationCity = new City { Name = "Destination" } };
        var service = new Service
        {
            ServiceId = 1,
            Name = "Mon 08:00",
            TripId = 1,
            Trip = trip,
            VehicleId = 1,
            DayOfWeek = today.DayOfWeek,
            DepartureHour = TimeSpan.FromHours(8),
            EstimatedDuration = TimeSpan.FromHours(2),
            Status = EntityStatusEnum.Active
        };

        var existingReserve = new Reserve
        {
            ReserveId = 99,
            TripId = 1,
            ReserveDate = today.Date,
            DepartureHour = TimeSpan.FromHours(8),
            Status = ReserveStatusEnum.Confirmed
        };

        var reserves = new List<Reserve> { existingReserve };

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }));
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves));
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(new List<Holiday>()));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        await _serviceBusiness.GenerateFutureReservesAsync();

        // 3 Mondays in window, but the first one already exists → only 2 new ones
        reserves.Where(r => r.ReserveId != 99).Should().HaveCount(2);
    }
}
