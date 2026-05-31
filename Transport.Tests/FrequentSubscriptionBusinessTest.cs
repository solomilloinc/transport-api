using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using System.Data;
using Transport.Business.Data;
using Transport.Business.FrequentSubscriptionBusiness;
using Transport.Domain.Customers;
using Transport.Domain.Directions;
using Transport.Domain.FrequentSubscriptions;
using Transport.Domain.FrequentSubscriptions.Abstraction;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.FrequentSubscription;
using Xunit;

namespace Transport.Tests.FrequentSubscriptionBusinessTests;

public class FrequentSubscriptionBusinessTest : TestBase
{
    private readonly Mock<IApplicationDbContext> _ctx;
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IDateTimeProvider> _clock;
    private readonly Mock<IFrequentPassengerBusiness> _passengers;
    private readonly FrequentSubscriptionBusiness _business;

    public FrequentSubscriptionBusinessTest()
    {
        _ctx = new Mock<IApplicationDbContext>();
        _uow = new Mock<IUnitOfWork>();
        _clock = new Mock<IDateTimeProvider>();
        _clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 05, 17));
        _clock.Setup(c => c.LocalNow).Returns(new DateTime(2026, 05, 17));

        _passengers = new Mock<IFrequentPassengerBusiness>();
        _passengers.Setup(p => p.GenerateForSubscriptionAsync(It.IsAny<int>()))
                   .ReturnsAsync(Result.Success(true));

        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((fn, _) => fn());

        _business = new FrequentSubscriptionBusiness(_ctx.Object, _uow.Object, _clock.Object, _passengers.Object);
    }

    private static FrequentSubscriptionCreateRequestDto BuildIda(
        int customerId = 1, int outboundServiceId = 10, int pickup = 100, int dropoff = 101) =>
        new(
            CustomerId: customerId,
            ReserveTypeId: (int)ReserveTypeIdEnum.Ida,
            OutboundServiceId: outboundServiceId,
            InboundServiceId: null,
            OutboundPickupLocationId: pickup,
            OutboundDropoffLocationId: dropoff,
            InboundPickupLocationId: null,
            InboundDropoffLocationId: null,
            StartDate: null,
            EndDate: null);

    private static Customer ActiveCustomer(int id = 1) => new()
    {
        CustomerId = id,
        FirstName = "María",
        LastName = "Pérez",
        Email = "m@p.com",
        DocumentNumber = "123",
        Phone1 = "555",
        Status = EntityStatusEnum.Active
    };

    private static Service ActiveService(int id, int vehicleCapacity = 10, int? allowedDirection = null)
    {
        var service = new Service
        {
            ServiceId = id,
            Status = EntityStatusEnum.Active,
            VehicleId = id * 10,
            Vehicle = new Vehicle { VehicleId = id * 10, AvailableQuantity = vehicleCapacity, Status = EntityStatusEnum.Active }
        };
        if (allowedDirection.HasValue)
            service.AllowedDirections.Add(new ServiceDirection { ServiceId = id, DirectionId = allowedDirection.Value });
        return service;
    }

    [Fact]
    public async Task Create_ShouldFail_WhenIdaConfigCarriesInboundFields()
    {
        var dto = BuildIda() with { InboundServiceId = 99 };

        var result = await _business.Create(dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FrequentSubscription.InvalidIdaConfig");
    }

    [Fact]
    public async Task Create_ShouldFail_WhenIdaVueltaMissingInboundFields()
    {
        var dto = BuildIda() with
        {
            ReserveTypeId = (int)ReserveTypeIdEnum.IdaVuelta,
            InboundServiceId = 20,
            InboundPickupLocationId = null,
            InboundDropoffLocationId = null
        };

        var result = await _business.Create(dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FrequentSubscription.InvalidIdaVueltaConfig");
    }

    [Fact]
    public async Task Create_ShouldFail_WhenCustomerNotFound()
    {
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer>()));

        var result = await _business.Create(BuildIda(customerId: 99));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CustomerError.NotFound);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenServiceNotActive()
    {
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { ActiveCustomer() }));
        _ctx.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service>()));

        var result = await _business.Create(BuildIda());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Service.ServiceNotActive");
    }

    [Fact]
    public async Task Create_ShouldFail_WhenDirectionNotAllowed()
    {
        var service = ActiveService(10, allowedDirection: 999);
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { ActiveCustomer() }));
        _ctx.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));

        var result = await _business.Create(BuildIda(pickup: 100, dropoff: 101));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FrequentSubscription.DirectionNotAllowedForService");
    }

    [Fact]
    public async Task Create_ShouldFail_WhenDuplicateActiveSub()
    {
        var service = ActiveService(10);
        var existing = new FrequentSubscription
        {
            CustomerId = 1, OutboundServiceId = 10,
            ReserveTypeId = ReserveTypeIdEnum.Ida,
            Status = EntityStatusEnum.Active
        };

        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { ActiveCustomer() }));
        _ctx.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { existing }));

        var result = await _business.Create(BuildIda());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FrequentSubscription.OverlapWithExistingSubscription");
    }

    [Fact]
    public async Task Create_ShouldFail_WhenCapacityExceeded()
    {
        var service = ActiveService(10, vehicleCapacity: 1);
        var existing = new FrequentSubscription
        {
            CustomerId = 2, OutboundServiceId = 10,
            ReserveTypeId = ReserveTypeIdEnum.Ida,
            Status = EntityStatusEnum.Active
        };

        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { ActiveCustomer() }));
        _ctx.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { existing }));

        var result = await _business.Create(BuildIda(customerId: 1));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FrequentSubscription.CapacityExceeded");
    }

    [Fact]
    public async Task Create_ShouldSucceed_WhenIdaValid()
    {
        var service = ActiveService(10);
        var stored = new List<FrequentSubscription>();
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { ActiveCustomer() }));
        _ctx.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetMockDbSetWithIdentity(stored));
        SetupSaveChangesWithOutboxAsync(_ctx);

        var result = await _business.Create(BuildIda());

        result.IsSuccess.Should().BeTrue();
        stored.Should().HaveCount(1);
        stored[0].ReserveTypeId.Should().Be(ReserveTypeIdEnum.Ida);
        stored[0].StartDate.Should().Be(new DateTime(2026, 05, 17));
    }

    [Fact]
    public async Task Create_ShouldAutoApply_AfterPersistingSubscription()
    {
        var service = ActiveService(10);
        var stored = new List<FrequentSubscription>();
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { ActiveCustomer() }));
        _ctx.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetMockDbSetWithIdentity(stored));
        SetupSaveChangesWithOutboxAsync(_ctx);

        var result = await _business.Create(BuildIda());

        result.IsSuccess.Should().BeTrue();
        // El auto-apply tiene que dispararse con el id recién creado, exactamente 1 vez.
        _passengers.Verify(p => p.GenerateForSubscriptionAsync(result.Value), Times.Once);
    }

    [Fact]
    public async Task Create_ShouldStillSucceed_WhenAutoApplyFails()
    {
        // Si el auto-apply falla por race u otra razón, la sub queda persistida igual y
        // el próximo run del batch la pickea — no queremos romper el Create.
        _passengers.Setup(p => p.GenerateForSubscriptionAsync(It.IsAny<int>()))
                   .ReturnsAsync(Result.Failure<bool>(FrequentSubscriptionError.NotFound));

        var service = ActiveService(10);
        var stored = new List<FrequentSubscription>();
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { ActiveCustomer() }));
        _ctx.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetMockDbSetWithIdentity(stored));
        SetupSaveChangesWithOutboxAsync(_ctx);

        var result = await _business.Create(BuildIda());

        result.IsSuccess.Should().BeTrue();
        stored.Should().HaveCount(1);
    }

    [Fact]
    public async Task Update_ShouldAutoApply_AfterPersistingChanges()
    {
        // Sub Ida activa que ya existe. El admin la edita (extiende EndDate). El Update
        // tiene que disparar el mismo auto-apply que Create — idempotente, pickea Reserves
        // que ahora caen en la nueva ventana sin tocar Passengers ya existentes.
        var service = ActiveService(10);
        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 42,
            CustomerId = 1,
            OutboundServiceId = 10,
            OutboundService = service,
            OutboundPickupLocationId = 100,
            OutboundDropoffLocationId = 101,
            ReserveTypeId = ReserveTypeIdEnum.Ida,
            StartDate = new DateTime(2026, 05, 01),
            EndDate = new DateTime(2026, 06, 01),
            Status = EntityStatusEnum.Active
        };
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));
        SetupSaveChangesWithOutboxAsync(_ctx);

        var dto = new FrequentSubscriptionUpdateRequestDto(
            OutboundPickupLocationId: 100,
            OutboundDropoffLocationId: 101,
            InboundPickupLocationId: null,
            InboundDropoffLocationId: null,
            StartDate: null,
            EndDate: new DateTime(2026, 12, 31)); // extiende endDate

        var result = await _business.Update(42, dto);

        result.IsSuccess.Should().BeTrue();
        subscription.EndDate.Should().Be(new DateTime(2026, 12, 31));
        _passengers.Verify(p => p.GenerateForSubscriptionAsync(42), Times.Once);
    }

    [Fact]
    public async Task Update_ShouldStillSucceed_WhenAutoApplyFails()
    {
        // Mismo principio que Create: si el auto-apply post-Update falla, la edición queda
        // persistida igual. El próximo batch run pickea lo que faltó.
        _passengers.Setup(p => p.GenerateForSubscriptionAsync(It.IsAny<int>()))
                   .ReturnsAsync(Result.Failure<bool>(FrequentSubscriptionError.NotFound));

        var service = ActiveService(10);
        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 42,
            CustomerId = 1,
            OutboundServiceId = 10,
            OutboundService = service,
            OutboundPickupLocationId = 100,
            OutboundDropoffLocationId = 101,
            ReserveTypeId = ReserveTypeIdEnum.Ida,
            StartDate = new DateTime(2026, 05, 01),
            EndDate = new DateTime(2026, 06, 01),
            Status = EntityStatusEnum.Active
        };
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));
        SetupSaveChangesWithOutboxAsync(_ctx);

        var dto = new FrequentSubscriptionUpdateRequestDto(
            OutboundPickupLocationId: 200, OutboundDropoffLocationId: 201,
            InboundPickupLocationId: null, InboundDropoffLocationId: null,
            StartDate: null, EndDate: new DateTime(2026, 12, 31));

        var result = await _business.Update(42, dto);

        result.IsSuccess.Should().BeTrue();
        subscription.OutboundPickupLocationId.Should().Be(200); // edición persistida igual
    }

    [Fact]
    public async Task Create_ShouldReturnDirectionErrorWithLegAndKindDetails()
    {
        var service = ActiveService(10, allowedDirection: 50); // pickup OK, dropoff NO
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { ActiveCustomer() }));
        _ctx.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }));

        var dto = BuildIda(pickup: 50, dropoff: 999);

        var result = await _business.Create(dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FrequentSubscription.DirectionNotAllowedForService");
        result.Error.Details.Should().NotBeNull();
        result.Error.Details!["leg"].Should().Be("outbound");
        result.Error.Details!["kind"].Should().Be("dropoff");
    }

    [Fact]
    public async Task Cancel_ShouldFail_WhenAlreadyCancelled()
    {
        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 7,
            CustomerId = 1,
            Status = EntityStatusEnum.Deleted
        };
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));

        var result = await _business.Cancel(7);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(FrequentSubscriptionError.AlreadyCancelled);
    }

    [Fact]
    public async Task Cancel_ShouldRefund_ReserveDepartingLateTodayLocal_NotSkipAsPast()
    {
        // Borde de medianoche: LocalNow = 30-may 22:00 (en UTC ya es 31-may 01:00).
        // Una reserva que sale 30-may 23:30 TODAVÍA NO partió → debe cancelarse y refundarse.
        // Comparar contra UtcNow la trataría como pasada (23:30 < 01:00 del 31) y NO refundaría: error de plata.
        _clock.Setup(c => c.LocalNow).Returns(new DateTime(2026, 5, 30, 22, 0, 0, DateTimeKind.Unspecified));
        _clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 5, 31, 1, 0, 0, DateTimeKind.Utc));

        var customer = ActiveCustomer();
        customer.CurrentBalance = 500m;

        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 7,
            CustomerId = customer.CustomerId,
            ReserveTypeId = ReserveTypeIdEnum.Ida,
            OutboundServiceId = 10,
            Status = EntityStatusEnum.Active
        };

        var lateTodayReserve = new Reserve
        {
            ReserveId = 50,
            ReserveDate = new DateTime(2026, 5, 30, 23, 30, 0),
            Status = ReserveStatusEnum.Confirmed
        };
        var passenger = new Passenger
        {
            PassengerId = 100,
            ReserveId = 50,
            Reserve = lateTodayReserve,
            CustomerId = customer.CustomerId,
            FrequentSubscriptionId = 7,
            Price = 200m,
            Status = PassengerStatusEnum.Confirmed,
            FirstName = "x", LastName = "x", DocumentNumber = "x"
        };

        var transactions = new List<CustomerAccountTransaction>();
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));
        _ctx.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(new List<Passenger> { passenger }));
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }));
        _ctx.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(transactions));
        SetupSaveChangesWithOutboxAsync(_ctx);

        var result = await _business.Cancel(7);

        result.IsSuccess.Should().BeTrue();
        passenger.Status.Should().Be(PassengerStatusEnum.Cancelled, "todavía no partió, se cancela");
        customer.CurrentBalance.Should().Be(300m, "se refunda 200 (500 - 200), no se saltea como pasada");
        transactions.Should().ContainSingle(t => t.Type == TransactionType.Refund && t.RelatedReserveId == 50);
    }

    [Fact]
    public async Task GetCancelPreview_ShouldCount_ReserveDepartingLateTodayLocal()
    {
        // Mismo borde: la reserva de 30-may 23:30 debe contar como futura (no partió).
        _clock.Setup(c => c.LocalNow).Returns(new DateTime(2026, 5, 30, 22, 0, 0, DateTimeKind.Unspecified));
        _clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 5, 31, 1, 0, 0, DateTimeKind.Utc));

        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 7,
            CustomerId = 1,
            Status = EntityStatusEnum.Active
        };
        var lateTodayReserve = new Reserve { ReserveId = 1, ReserveDate = new DateTime(2026, 5, 30, 23, 30, 0) };
        var passengers = new List<Passenger>
        {
            new() { ReserveId = 1, Reserve = lateTodayReserve, FrequentSubscriptionId = 7, Price = 200m, Status = PassengerStatusEnum.Confirmed, FirstName="x", LastName="x", DocumentNumber="x" }
        };

        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));
        _ctx.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers));

        var result = await _business.GetCancelPreview(7);

        result.IsSuccess.Should().BeTrue();
        result.Value.PassengersToCancel.Should().Be(1, "23:30 local todavía no partió a las 22:00");
        result.Value.TotalRefundAmount.Should().Be(200m);
    }

    [Fact]
    public async Task GetCancelPreview_ShouldReturnCountAndTotal_ForFutureUntraveledPassengers()
    {
        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 7,
            CustomerId = 1,
            Status = EntityStatusEnum.Active
        };

        var futureReserveA = new Reserve { ReserveId = 1, ReserveDate = new DateTime(2026, 05, 25) };
        var futureReserveB = new Reserve { ReserveId = 2, ReserveDate = new DateTime(2026, 06, 01) };
        var pastReserve = new Reserve { ReserveId = 3, ReserveDate = new DateTime(2026, 05, 10) };

        var passengers = new List<Passenger>
        {
            new() { ReserveId = 1, Reserve = futureReserveA, FrequentSubscriptionId = 7, Price = 1000m, Status = PassengerStatusEnum.Confirmed, FirstName="x", LastName="x", DocumentNumber="x" },
            new() { ReserveId = 2, Reserve = futureReserveB, FrequentSubscriptionId = 7, Price = 1500m, Status = PassengerStatusEnum.Confirmed, FirstName="x", LastName="x", DocumentNumber="x" },
            // viajado: no se cuenta
            new() { ReserveId = 3, Reserve = pastReserve, FrequentSubscriptionId = 7, Price = 800m, Status = PassengerStatusEnum.Traveled, HasTraveled = true, FirstName="x", LastName="x", DocumentNumber="x" },
            // ya cancelado: no se cuenta
            new() { ReserveId = 1, Reserve = futureReserveA, FrequentSubscriptionId = 7, Price = 500m, Status = PassengerStatusEnum.Cancelled, FirstName="x", LastName="x", DocumentNumber="x" }
        };

        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));
        _ctx.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers));

        var result = await _business.GetCancelPreview(7);

        result.IsSuccess.Should().BeTrue();
        result.Value.FrequentSubscriptionId.Should().Be(7);
        result.Value.PassengersToCancel.Should().Be(2);
        result.Value.TotalRefundAmount.Should().Be(2500m);
    }

    [Fact]
    public async Task GetCancelPreview_ShouldFail_WhenAlreadyCancelled()
    {
        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 7,
            Status = EntityStatusEnum.Deleted
        };
        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));

        var result = await _business.GetCancelPreview(7);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(FrequentSubscriptionError.AlreadyCancelled);
    }

    [Fact]
    public async Task Cancel_ShouldCascade_FuturePassengersAndRevertCharges()
    {
        var customer = ActiveCustomer();
        customer.CurrentBalance = 500m;

        var subscription = new FrequentSubscription
        {
            FrequentSubscriptionId = 7,
            CustomerId = customer.CustomerId,
            ReserveTypeId = ReserveTypeIdEnum.Ida,
            OutboundServiceId = 10,
            Status = EntityStatusEnum.Active
        };

        var futureReserve = new Reserve
        {
            ReserveId = 50,
            ReserveDate = new DateTime(2026, 05, 25),
            Status = ReserveStatusEnum.Confirmed
        };
        var pastReserve = new Reserve
        {
            ReserveId = 51,
            ReserveDate = new DateTime(2026, 05, 10),
            Status = ReserveStatusEnum.Confirmed
        };

        var futurePassenger = new Passenger
        {
            PassengerId = 100,
            ReserveId = 50,
            Reserve = futureReserve,
            CustomerId = customer.CustomerId,
            FrequentSubscriptionId = 7,
            Price = 200m,
            Status = PassengerStatusEnum.Confirmed,
            FirstName = "x",
            LastName = "x",
            DocumentNumber = "x"
        };
        var pastPassenger = new Passenger
        {
            PassengerId = 101,
            ReserveId = 51,
            Reserve = pastReserve,
            CustomerId = customer.CustomerId,
            FrequentSubscriptionId = 7,
            Price = 200m,
            Status = PassengerStatusEnum.Traveled,
            HasTraveled = true,
            FirstName = "x",
            LastName = "x",
            DocumentNumber = "x"
        };

        var transactions = new List<CustomerAccountTransaction>();

        _ctx.Setup(c => c.FrequentSubscriptions).Returns(GetQueryableMockDbSet(new List<FrequentSubscription> { subscription }));
        _ctx.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(new List<Passenger> { futurePassenger, pastPassenger }));
        _ctx.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }));
        _ctx.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(transactions));
        SetupSaveChangesWithOutboxAsync(_ctx);

        var result = await _business.Cancel(7);

        result.IsSuccess.Should().BeTrue();
        subscription.Status.Should().Be(EntityStatusEnum.Deleted);
        futurePassenger.Status.Should().Be(PassengerStatusEnum.Cancelled);
        pastPassenger.Status.Should().Be(PassengerStatusEnum.Traveled);
        customer.CurrentBalance.Should().Be(300m);
        transactions.Should().HaveCount(1);
        transactions[0].Type.Should().Be(TransactionType.Refund);
        transactions[0].Amount.Should().Be(-200m);
    }
}
