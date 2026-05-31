using FluentAssertions;
using Moq;
using Transport.Business.Data;
using Transport.Business.FrequentSubscriptionBusiness;
using Transport.Domain.Cities;
using Transport.Domain.Customers;
using Transport.Domain.Directions;
using Transport.Domain.FrequentSubscriptions;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Tenants;
using Transport.Domain.Trips;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Configuration;
using Xunit;

namespace Transport.Tests.FrequentPassengerBusinessTests;

public class FrequentPassengerBusinessTest : TestBase
{
    private readonly Mock<IApplicationDbContext> _ctx;
    private readonly Mock<IReserveOption> _reserveOption;
    private readonly Mock<IDateTimeProvider> _clock;
    private readonly FrequentPassengerBusiness _business;

    private static readonly DateTime Today = new(2026, 05, 18); // Monday

    public FrequentPassengerBusinessTest()
    {
        _ctx = new Mock<IApplicationDbContext>();
        _reserveOption = new Mock<IReserveOption>();
        _reserveOption.Setup(r => r.ReserveGenerationDays).Returns(15);

        _clock = new Mock<IDateTimeProvider>();
        _clock.Setup(c => c.UtcNow).Returns(Today);
        _clock.Setup(c => c.LocalNow).Returns(Today);

        // TenantConfigs vacío por default → GetReserveGenerationDaysAsync cae al IReserveOption (15).
        // Cada test que quiera testear el override per-tenant puede sobreescribir este setup.
        _ctx.Setup(c => c.TenantConfigs).Returns(GetQueryableMockDbSet(new List<TenantConfig>()));

        _business = new FrequentPassengerBusiness(
            _ctx.Object,
            _reserveOption.Object,
            _clock.Object,
            new FakeTenantContext { TenantId = 1 });
    }

    private static Customer Maria => new()
    {
        CustomerId = 1,
        FirstName = "María",
        LastName = "Pérez",
        Email = "m@p.com",
        DocumentNumber = "123",
        Phone1 = "555",
        Status = EntityStatusEnum.Active
    };

    private static Service ServiceWith(int id, int tripId, int originCityId, int destinationCityId)
    {
        return new Service
        {
            ServiceId = id,
            TripId = tripId,
            VehicleId = 100,
            Status = EntityStatusEnum.Active,
            Trip = new Trip
            {
                TripId = tripId,
                OriginCityId = originCityId,
                DestinationCityId = destinationCityId,
                Status = EntityStatusEnum.Active,
                OriginCity = new City { CityId = originCityId, Name = "Origin" },
                DestinationCity = new City { CityId = destinationCityId, Name = "Destination" },
                Prices = new List<TripPrice>()
            }
        };
    }

    private static Reserve ReserveOn(int reserveId, int serviceId, DateTime date) => new()
    {
        ReserveId = reserveId,
        ServiceId = serviceId,
        VehicleId = 100,
        ReserveDate = date.Date,
        Status = ReserveStatusEnum.Confirmed
    };

    [Fact]
    public async Task Generate_ShouldDoNothing_WhenNoActiveSubscriptions()
    {
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription>()));
        SetupSaveChangesWithOutboxAsync(_ctx);

        var result = await _business.GenerateFrequentPassengersAsync();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Generate_ShouldCreateIdaPassenger_WhenSubscriptionAndReserveMatch()
    {
        var customer = Maria;
        var service = ServiceWith(id: 10, tripId: 1, originCityId: 1, destinationCityId: 2);
        service.Trip.Prices.Add(new TripPrice
        {
            TripPriceId = 1,
            TripId = 1,
            CityId = 2,
            ReserveTypeId = ReserveTypeIdEnum.Ida,
            Price = 1500m,
            Status = EntityStatusEnum.Active
        });

        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 1,
            CustomerId = 1,
            Customer = customer,
            ReserveTypeId = ReserveTypeIdEnum.Ida,
            OutboundServiceId = 10,
            OutboundService = service,
            OutboundPickupLocationId = 50,
            OutboundDropoffLocationId = 60,
            StartDate = Today,
            EndDate = null,
            Status = EntityStatusEnum.Active
        };

        var reserve = ReserveOn(reserveId: 99, serviceId: 10, date: Today);
        var passengers = new List<Passenger>();
        var transactions = new List<CustomerAccountTransaction>();

        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));
        _ctx.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve }));
        _ctx.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { service.Trip }));
        _ctx.Setup(c => c.Directions).Returns(GetQueryableMockDbSet(new List<Direction>()));
        _ctx.Setup(c => c.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle> { new() { VehicleId = 100, AvailableQuantity = 10 } }));
        _ctx.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers));
        _ctx.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(transactions));
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }));
        SetupSaveChangesWithOutboxAsync(_ctx);

        await _business.GenerateFrequentPassengersAsync();

        passengers.Should().HaveCount(1);
        passengers[0].CustomerId.Should().Be(1);
        passengers[0].FrequentSubscriptionId.Should().Be(1);
        // PendingPayment (no Confirmed) para que el flow estándar de saldar deuda los encuentre.
        passengers[0].Status.Should().Be(PassengerStatusEnum.PendingPayment);
        passengers[0].Price.Should().Be(1500m);
        passengers[0].ReserveRelatedId.Should().BeNull();

        transactions.Should().HaveCount(1);
        transactions[0].Type.Should().Be(TransactionType.Charge);
        transactions[0].Amount.Should().Be(1500m);
        customer.CurrentBalance.Should().Be(1500m);
    }

    [Fact]
    public async Task Generate_ShouldSkipExisting_WhenPassengerAlreadyCreated()
    {
        var customer = Maria;
        var service = ServiceWith(id: 10, tripId: 1, originCityId: 1, destinationCityId: 2);
        service.Trip.Prices.Add(new TripPrice { TripPriceId = 1, TripId = 1, CityId = 2, ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100m, Status = EntityStatusEnum.Active });

        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 1,
            CustomerId = 1,
            Customer = customer,
            ReserveTypeId = ReserveTypeIdEnum.Ida,
            OutboundServiceId = 10,
            OutboundService = service,
            OutboundPickupLocationId = 50,
            OutboundDropoffLocationId = 60,
            StartDate = Today,
            Status = EntityStatusEnum.Active
        };

        var reserve = ReserveOn(reserveId: 99, serviceId: 10, date: Today);
        var existingPassenger = new Passenger
        {
            PassengerId = 5,
            ReserveId = 99,
            CustomerId = 1,
            FrequentSubscriptionId = 1,
            Status = PassengerStatusEnum.Confirmed,
            Price = 100m,
            FirstName = "x", LastName = "x", DocumentNumber = "x"
        };

        var passengers = new List<Passenger> { existingPassenger };
        var transactions = new List<CustomerAccountTransaction>();

        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));
        _ctx.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve }));
        _ctx.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { service.Trip }));
        _ctx.Setup(c => c.Directions).Returns(GetQueryableMockDbSet(new List<Direction>()));
        _ctx.Setup(c => c.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle> { new() { VehicleId = 100, AvailableQuantity = 10 } }));
        _ctx.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers));
        _ctx.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(transactions));
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }));
        SetupSaveChangesWithOutboxAsync(_ctx);

        await _business.GenerateFrequentPassengersAsync();

        passengers.Should().HaveCount(1); // no extra
        transactions.Should().BeEmpty();
        customer.CurrentBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Generate_ShouldCreateLinkedPassengers_WithIdaVueltaPromo_WhenSameDay()
    {
        var customer = Maria;
        var outboundService = ServiceWith(id: 10, tripId: 1, originCityId: 1, destinationCityId: 2);
        outboundService.Trip.Prices.Add(new TripPrice
        {
            TripPriceId = 1, TripId = 1, CityId = 2,
            ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Price = 2400m, Status = EntityStatusEnum.Active
        });
        var inboundService = ServiceWith(id: 20, tripId: 2, originCityId: 2, destinationCityId: 1);

        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 1,
            CustomerId = 1, Customer = customer,
            ReserveTypeId = ReserveTypeIdEnum.IdaVuelta,
            OutboundServiceId = 10, OutboundService = outboundService,
            InboundServiceId = 20, InboundService = inboundService,
            OutboundPickupLocationId = 50, OutboundDropoffLocationId = 60,
            InboundPickupLocationId = 70, InboundDropoffLocationId = 80,
            StartDate = Today, Status = EntityStatusEnum.Active
        };

        var outboundReserve = ReserveOn(reserveId: 100, serviceId: 10, date: Today);
        var inboundReserve = ReserveOn(reserveId: 101, serviceId: 20, date: Today);

        var passengers = new List<Passenger>();
        var transactions = new List<CustomerAccountTransaction>();

        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));
        _ctx.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { outboundReserve, inboundReserve }));
        _ctx.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { outboundService.Trip, inboundService.Trip }));
        _ctx.Setup(c => c.Directions).Returns(GetQueryableMockDbSet(new List<Direction>()));
        _ctx.Setup(c => c.Vehicles).Returns(GetQueryableMockDbSet(new List<Vehicle> { new() { VehicleId = 100, AvailableQuantity = 10 } }));
        _ctx.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers));
        _ctx.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(transactions));
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }));
        SetupSaveChangesWithOutboxAsync(_ctx);

        await _business.GenerateFrequentPassengersAsync();

        passengers.Should().HaveCount(2);
        passengers[0].ReserveId.Should().Be(100);
        passengers[0].ReserveRelatedId.Should().Be(101);
        passengers[1].ReserveId.Should().Be(101);
        passengers[1].ReserveRelatedId.Should().Be(100);

        // Convención IdaVuelta package (Opción D): Outbound se lleva el packagePrice, Inbound queda en 0.
        passengers[0].Price.Should().Be(2400m);
        passengers[1].Price.Should().Be(0m);

        transactions.Should().HaveCount(1);
        transactions[0].Amount.Should().Be(2400m);
        customer.CurrentBalance.Should().Be(2400m);
    }
}
