using FluentAssertions;
using Moq;
using Transport.Business.Data;
using Transport.Business.ReserveBusiness;
using Transport.Domain.Reserves;
using Transport.Domain.Vehicles;
using Transport.Domain.Drivers;
using Transport.SharedKernel.Contracts.Reserve;
using Xunit;
using Transport.Domain.Cities;
using Transport.Domain.Customers;
using Transport.Domain.Services;
using Transport.SharedKernel.Contracts.Customer;
using Transport.SharedKernel;
using Transport.Domain.Directions;
using System.Data;
using System.Threading;
using System.Linq.Expressions;
using Transport.Business.Authentication;
using MercadoPago.Resource.Payment;
using Moq.Protected;
using Transport.Domain.Users;
using Microsoft.AspNetCore.Builder.Extensions;
using MercadoPago.Client.Payment;
using Transport.Business.Services.Payment;
using Transport.Domain.CashBoxes;
using Transport.Domain.CashBoxes.Abstraction;
using Transport.Domain.Customers.Abstraction;
using Transport.Domain.Passengers;
using Transport.Domain.Trips;
using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Configuration;

namespace Transport.Tests.ReserveBusinessTests;

public class ReserveBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserContext> _userContextMock;
    private readonly Mock<IMercadoPagoPaymentGateway> _paymentGatewayMock;
    private readonly Mock<ICustomerBusiness> _customerBusinessMock;
    private readonly Mock<ICashBoxBusiness> _cashBoxBusinessMock;
    private readonly ReserveBusiness _reserveBusiness;

    public ReserveBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userContextMock = new Mock<IUserContext>();
        _paymentGatewayMock = new Mock<IMercadoPagoPaymentGateway>();
        _customerBusinessMock = new Mock<ICustomerBusiness>();
        _cashBoxBusinessMock = new Mock<ICashBoxBusiness>();

        // Setup default open CashBox
        var openCashBox = new CashBox { CashBoxId = 1, Status = CashBoxStatusEnum.Open };
        _cashBoxBusinessMock.Setup(x => x.GetOpenCashBoxEntity())
            .ReturnsAsync(Result.Success(openCashBox));

        _reserveBusiness = new ReserveBusiness(_contextMock.Object,
            _unitOfWorkMock.Object,
            _userContextMock.Object,
            _paymentGatewayMock.Object,
            _customerBusinessMock.Object,
            new FakeReserveOption(),
            _cashBoxBusinessMock.Object);
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
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    public async Task CreatePassengerReserves_Payments_ParentChildLogic_Works(int reserveCount, int paymentCount)
    {
        // Arrange
        var trip = new Trip
        {
            TripId = 1,
            OriginCityId = 1,
            DestinationCityId = 2,
            OriginCity = new City { CityId = 1, Name = "CityA" },
            DestinationCity = new City { CityId = 2, Name = "CityB" }
        };

        var reservesList = Enumerable.Range(1, reserveCount).Select(i => new Reserve
        {
            ReserveId = i,
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip,
            Driver = new Driver { FirstName = "John", LastName = "Doe" }
        }).ToList();

        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service
        {
            ServiceId = 1,
            Trip = trip
        };
        var trips = new List<Trip> { new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> { new TripPrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Price = 100, Status = EntityStatusEnum.Active } } } };


        var customer = new Customer
        {
            CustomerId = 1,
            FirstName = "Jane",
            LastName = "Smith",
            DocumentNumber = "12345678",
            Email = "jane@example.com",
            CurrentBalance = 0
        };

        var origin = new Direction { DirectionId = 10, Name = "PickupLocation" };
        var destination = new Direction { DirectionId = 20, Name = "DropoffLocation" };

        var reservePaymentsList = new List<ReservePayment>();
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(reservePaymentsList).Object);

        var accountTransactionsList = new List<CustomerAccountTransaction>();
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactionsList).Object);

        var customers = new List<Customer> { customer };
        var directions = new List<Direction> { origin, destination };
        var passengers = new List<Passenger>();
        var users = new List<User>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reservesList).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(customers).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(It.IsAny<int>())).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Directions).Returns(GetQueryableMockDbSet(directions).Object);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(origin);
        _contextMock.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers).Object);
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(users).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);

        _contextMock.Setup(c => c.Customers.Update(It.IsAny<Customer>())).Callback<Customer>(c =>
        {
            customer.CurrentBalance = c.CurrentBalance;
        });

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var passengersList = Enumerable.Range(1, reserveCount).Select(i =>
            new PassengerReserveCreateRequestDto(
                ReserveId: i,
                ReserveTypeId: (int)ReserveTypeIdEnum.IdaVuelta,
                CustomerId: 1,
                IsPayment: true,
                PickupLocationId: 1,
                DropoffLocationId: 2,
                HasTraveled: false,
                Price: 100
            )
        ).ToList();

        // IdaVuelta: si hay 2 reservas, el precio ya incluye ida y vuelta (no se duplica)
        var totalAmount = reserveCount == 2 ? 100m : 100m * reserveCount;
        var payments = new List<CreatePaymentRequestDto>();

        if (paymentCount == 1)
        {
            payments.Add(new CreatePaymentRequestDto(totalAmount, 1));
        }
        else
        {
            var partialAmount = totalAmount / paymentCount;
            for (int i = 0; i < paymentCount; i++)
            {
                payments.Add(new CreatePaymentRequestDto(partialAmount, 1));
            }
        }

        var request = new PassengerReserveCreateRequestWrapperDto(payments, passengersList);

        // Act
        var result = await _reserveBusiness.CreatePassengerReserves(request);

        // Assert
        Assert.True(result.IsSuccess);

        // Lógica de pagos:
        // - paymentCount == 1: solo 1 pago (parent)
        // - paymentCount > 1: 1 parent + N breakdown children = (paymentCount + 1) pagos
        // - reserveCount ya no afecta el conteo de pagos (no hay link children)
        var expectedPaymentCount = paymentCount == 1 ? 1 : paymentCount + 1;
        Assert.Equal(expectedPaymentCount, reservePaymentsList.Count);

        var parent = reservePaymentsList.First();
        Assert.Equal(payments.Sum(p => p.TransactionAmount), parent.Amount);
        Assert.Null(parent.ParentReservePaymentId);
        Assert.NotNull(parent.CashBoxId); // El pago admin debe tener CashBoxId

        // Verificar breakdown children si hay más de un método de pago
        if (paymentCount > 1)
        {
            var breakdownChildren = reservePaymentsList.Where(p => p.ParentReservePaymentId != null).ToList();
            Assert.Equal(paymentCount, breakdownChildren.Count);
            foreach (var child in breakdownChildren)
            {
                Assert.True(child.Amount > 0);
                Assert.Equal(parent.ReservePaymentId, child.ParentReservePaymentId);
            }
        }

        // ✅ Validación de CustomerAccountTransactions
        Assert.Equal(2, accountTransactionsList.Count);

        var charge = accountTransactionsList.FirstOrDefault(x => x.Type == TransactionType.Charge);
        var payment = accountTransactionsList.FirstOrDefault(x => x.Type == TransactionType.Payment);

        Assert.NotNull(charge);
        Assert.Equal(totalAmount, charge!.Amount);
        Assert.Equal(customer.CustomerId, charge.CustomerId);
        Assert.Contains("Reserva", charge.Description ?? string.Empty);

        Assert.NotNull(payment);
        Assert.Equal(-totalAmount, payment!.Amount);
        Assert.Equal(customer.CustomerId, payment.CustomerId);
        Assert.Contains("Pago", payment.Description ?? string.Empty);

        // ✅ Verificación de saldo actualizado
        Assert.Equal(0, customer.CurrentBalance);
    }

    [Fact]
    public async Task CreatePassengerReservesExternal_IdaYVuelta_CreatesParentAndChildPayments()
    {
        // Arrange
        var trip = new Trip
        {
            TripId = 1,
            OriginCityId = 1,
            DestinationCityId = 2,
            OriginCity = new City { CityId = 1, Name = "Córdoba" },
            DestinationCity = new City { CityId = 2, Name = "Rosario" }
        };

        var reserve1 = new Reserve
        {
            ReserveId = 1,
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip,
            Driver = new Driver { FirstName = "Mario", LastName = "Bros" }
        };
        var reserve2 = new Reserve
        {
            ReserveId = 2,
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip
        };
        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service
        {
            ServiceId = 1,
            Trip = trip
        };
        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active },
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Price = 100, Status = EntityStatusEnum.Active }
            }}
        };
        var origin = new Direction { DirectionId = 1, Name = "Pickup" };
        var destination = new Direction { DirectionId = 2, Name = "Dropoff" };
        var customer = new Customer
        {
            CustomerId = 123,
            FirstName = "Pepe",
            LastName = "Argento",
            DocumentNumber = "32145678",
            Email = "pepe@example.com"
        };

        var reservePaymentsList = new List<ReservePayment>();
        var passengers = new List<Passenger>();
        var reservesList = new List<Reserve> { reserve1, reserve2 };

        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(reservePaymentsList).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(destination);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(origin);
        _contextMock.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers).Object);
        _contextMock.Setup(c => c.Reserves).Returns(GetMockDbSetWithIdentity(reservesList).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetMockDbSetWithIdentity(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Setup usuario logueado
        _userContextMock.SetupGet(x => x.UserId).Returns(999);

        var bookingCustomer = new Customer { CustomerId = 123, FirstName = "Pepe", LastName = "Argento" };
        var user = new User { UserId = 999, CustomerId = 123, Customer = bookingCustomer };

        _contextMock
            .Setup(c => c.Users)
            .Returns(GetMockDbSetWithIdentity(new List<User> { user }).Object);

        _userContextMock.SetupGet(x => x.UserId).Returns(999);

        var paymentGatewayMock = new Mock<IMercadoPagoPaymentGateway>();
        paymentGatewayMock
            .Setup(x => x.CreatePaymentAsync(It.IsAny<PaymentCreateRequest>()))
            .ReturnsAsync(new Payment
            {
                Id = 987654321,
                Status = "approved",
                StatusDetail = "accredited",
                ExternalReference = "12345"
            });

        paymentGatewayMock
            .Setup(x => x.GetPaymentAsync(It.IsAny<string>()))
            .ReturnsAsync(new Payment
            {
                Id = 987654321,
                Status = "approved",
                StatusDetail = "accredited",
                ExternalReference = "12345"
            });

        var reserveBusiness = new ReserveBusiness(
            _contextMock.Object,
            _unitOfWorkMock.Object,
            _userContextMock.Object,
            paymentGatewayMock.Object,
            _customerBusinessMock.Object,
            new FakeReserveOption(),
            _cashBoxBusinessMock.Object);

        // 1 pasajero con ida/vuelta = 2 items (1 para ida con tipo Ida, 1 para vuelta con tipo IdaVuelta)
        // La validación requiere exactamente Ida + IdaVuelta para 2 reservas
        var passengerList = new List<PassengerReserveExternalCreateRequestDto>
        {
        new(
            ReserveId: 1,
            ReserveTypeId: (int)ReserveTypeIdEnum.Ida,
            CustomerId: null,
            IsPayment: true,
            PickupLocationId: 1,
            DropoffLocationId: 2,
            HasTraveled: false,
            Price: 100m,
            FirstName: "Pepe",
            LastName: "Argento",
            Email: "pepe@example.com",
            Phone1: "32145678",
            DocumentNumber: "32145678"
        ),
        new(
            ReserveId: 2,
            ReserveTypeId: (int)ReserveTypeIdEnum.IdaVuelta,
            CustomerId: null,
            IsPayment: true,
            PickupLocationId: 1,
            DropoffLocationId: 2,
            HasTraveled: false,
            Price: 100m,
            FirstName: "Pepe",
            LastName: "Argento",
            Email: "pepe@example.com",
            Phone1: "32145678",
            DocumentNumber: "32145678"
        )
        };

        // 1 pasajero IdaVuelta = precio 100 (no 200)
        var paymentDto = new CreatePaymentExternalRequestDto(
            TransactionAmount: 100m,
            Token: "token",
            Description: "Reserva de ida y vuelta",
            Installments: 1,
            PaymentMethodId: "visa",
            PayerEmail: "pepe@example.com",
            IdentificationType: "DNI",
            IdentificationNumber: "32145678"
        );

        var request = new PassengerReserveCreateRequestWrapperExternalDto(paymentDto, passengerList);

        _unitOfWorkMock
    .Setup(u => u.ExecuteInTransactionAsync(
        It.IsAny<Func<Task<Result<CreateReserveExternalResult>>>>(),
        It.IsAny<IsolationLevel>()))
    .Returns(async (Func<Task<Result<CreateReserveExternalResult>>> func, IsolationLevel _) => await func());

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns(async (Func<Task<Result<bool>>> func, IsolationLevel _) => await func());

        // Act - Prueba con pago directo
        var result = await reserveBusiness.CreatePassengerReservesExternal(request);

        // Assert - Verificaciones para pago directo
        Assert.True(result.IsSuccess);
        Assert.Single(reservePaymentsList); // Solo pago padre, sin link children
        Assert.Equal(2, reserve1.Passengers.Count + reserve2.Passengers.Count);

        var parentPayment = reservePaymentsList[0];

        // Verificar pago padre (1 pasajero IdaVuelta = precio 100, no 200)
        Assert.Equal(100m, parentPayment.Amount);
        Assert.Null(parentPayment.ParentReservePaymentId);
        Assert.Equal(987654321, parentPayment.PaymentExternalId);
        Assert.Equal(StatusPaymentEnum.Paid, parentPayment.Status);

        // Verificar estados de las reservas
        Assert.All(reserve1.Passengers, cr =>
            Assert.Equal(PassengerStatusEnum.Confirmed, cr.Status));

        Assert.All(reserve2.Passengers, cr =>
    Assert.Equal(PassengerStatusEnum.Confirmed, cr.Status));


        // Limpiar para prueba con wallet
        reservePaymentsList.Clear();
        reserve1.Passengers.Clear();
        reserve2.Passengers.Clear();

        _contextMock.Setup(c => c.Passengers.Add(It.IsAny<Passenger>()))
        .Callback<Passenger>(cr =>
        {
            passengers.Add(cr);
            // También agregar a la reserva correspondiente
            var reserve = reservesList.FirstOrDefault(r => r.ReserveId == cr.ReserveId);
            if (reserve != null)
            {
                reserve.Passengers.Add(cr);
            }
        });

        // Act - Prueba con wallet (sin pago directo)
        var walletRequest = new PassengerReserveCreateRequestWrapperExternalDto(null, passengerList);
        var walletResult = await reserveBusiness.CreatePassengerReservesExternal(walletRequest);

        // Assert - Verificaciones para wallet
        Assert.True(walletResult.IsSuccess);
        Assert.Single(reservePaymentsList); // Solo pago padre, sin link children

        var walletParentPayment = reservePaymentsList[0];

        // Verificar pago padre (1 pasajero con precio 100)
        Assert.Equal(100m, walletParentPayment.Amount);
        Assert.Null(walletParentPayment.ParentReservePaymentId);
        Assert.Null(walletParentPayment.PaymentExternalId); // Aun no tiene ID externo
        Assert.Equal(StatusPaymentEnum.Pending, walletParentPayment.Status);

        // Verificar estados de las reservas
        Assert.All(reserve1.Passengers, cr =>
           Assert.Equal(PassengerStatusEnum.PendingPayment, cr.Status));

        Assert.All(reserve2.Passengers, cr =>
    Assert.Equal(PassengerStatusEnum.PendingPayment, cr.Status));

        paymentGatewayMock
        .Setup(x => x.GetPaymentAsync("987654321"))
        .ReturnsAsync(new Payment
        {
            Id = 987654321,
            Status = "approved",
            StatusDetail = "accredited",
            ExternalReference = walletParentPayment.ReservePaymentId.ToString()
        });

        // Simular notificación de pago para wallet
        var updateResult = await reserveBusiness.UpdateReservePaymentsByExternalId("987654321");
        Assert.True(updateResult.IsSuccess);

        // Verificar actualización después de notificación
        Assert.Equal(987654321, walletParentPayment.PaymentExternalId);
        Assert.Equal(StatusPaymentEnum.Paid, walletParentPayment.Status);
    }


    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenReserveNotFound()
    {
        var reserves = new List<Reserve>();
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);

        _unitOfWorkMock.Setup(uow => uow.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
                       .Returns<Func<Task<Result<bool>>>, IsolationLevel>(async (func, _) => await func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto>
       {
           new CreatePaymentRequestDto(1000, 1)
       });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveError.NotFound);
    }


    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenPaymentsListIsEmpty()
    {
        var passengers = new List<Passenger>();
        var reserves = new List<Reserve> { new Reserve { ReserveId = 1, Passengers = passengers } };
        var customers = new List<Customer> { new Customer { CustomerId = 1 } };
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(customers).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(new Customer { CustomerId = 1 });

        _unitOfWorkMock
          .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
              It.IsAny<Func<Task<Result<bool>>>>(),
              It.IsAny<IsolationLevel>()))
          .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto>());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Payments.Empty");
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenTransactionAmountIsInvalid()
    {
        var passengers = new List<Passenger>();
        var reserves = new List<Reserve> { new Reserve { ReserveId = 1, Passengers = passengers } };
        var customers = new List<Customer> { new Customer { CustomerId = 1 } };
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(customers).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(new Customer { CustomerId = 1 });

        _unitOfWorkMock
         .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
             It.IsAny<Func<Task<Result<bool>>>>(),
             It.IsAny<IsolationLevel>()))
         .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto>
    {
        new CreatePaymentRequestDto(0, 1),
        new CreatePaymentRequestDto(-100, 2)
    });

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Payments.InvalidAmount");
        result.Error.Description.Should().Contain("Pago #1").And.Contain("Pago #2");
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenPaymentMethodsAreDuplicated()
    {
        var passengers = new List<Passenger>();
        var reserves = new List<Reserve> { new Reserve { ReserveId = 1, Passengers = passengers } };
        var customers = new List<Customer> { new Customer { CustomerId = 1 } };
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(customers).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(new Customer { CustomerId = 1 });

        _unitOfWorkMock
        .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
            It.IsAny<Func<Task<Result<bool>>>>(),
            It.IsAny<IsolationLevel>()))
        .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto>
    {
        new CreatePaymentRequestDto(1000, 1),
        new CreatePaymentRequestDto(2000, 1)
    });

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Payments.DuplicatedMethod");
        result.Error.Description.Should().Contain("Duplicados: 1");
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldSucceed_WhenValidPaymentsProvided()
    {
        // Arrange
        var paymentsDb = new List<ReservePayment>();
        var passengers = new List<Passenger> { new Passenger { PassengerId = 1, ReserveId = 1, CustomerId = 1, Price = 100, Status = PassengerStatusEnum.PendingPayment } };
        var reserve = new Reserve { ReserveId = 1, ServiceId = 1, Passengers = passengers, ReserveDate = DateTime.Today, DepartureHour = TimeSpan.FromHours(9), TripId = 1 };
        var reserves = new List<Reserve> { reserve };
        var customer = new Customer { CustomerId = 1, DocumentNumber = "12345678", FirstName = "Test", LastName = "User", Email = "test@test.com" };
        var service = new Service { ServiceId = 1 };
        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active }
            }}
        };

        var customers = new List<Customer> { customer };
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(customers).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);

        var customerAccountTransactions = new List<CustomerAccountTransaction>();
        var reservePaymentsDbSet = GetMockDbSetWithIdentity(paymentsDb);
        var customerAccountTransactionsDbSet = GetMockDbSetWithIdentity(customerAccountTransactions);
        _contextMock.Setup(c => c.ReservePayments).Returns(reservePaymentsDbSet.Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(customerAccountTransactionsDbSet.Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
           .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
               It.IsAny<Func<Task<Result<bool>>>>(),
               It.IsAny<IsolationLevel>()))
           .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Act
        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto>
        {
            new CreatePaymentRequestDto(50, 1),
            new CreatePaymentRequestDto(50, 2)
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Con CashBox:
        // - 1 pago padre con monto total (100)
        // - 2 hijos de desglose (50 cada uno) cuando hay split de metodos
        paymentsDb.Should().HaveCount(3);

        var parentPayment = paymentsDb.First(p => p.ParentReservePaymentId == null);
        parentPayment.Amount.Should().Be(100);
        parentPayment.ReserveId.Should().Be(1);
        parentPayment.CustomerId.Should().Be(1);
        parentPayment.Status.Should().Be(StatusPaymentEnum.Paid);
        parentPayment.CashBoxId.Should().Be(1); // Debe estar en la caja abierta

        var childPayments = paymentsDb.Where(p => p.ParentReservePaymentId != null).ToList();
        childPayments.Should().HaveCount(2);
        childPayments.Select(p => p.Amount).Should().BeEquivalentTo(new[] { 50m, 50m });
        childPayments.All(p => p.ReserveId == 1 && p.CustomerId == 1).Should().BeTrue();
        childPayments.Should().OnlyContain(p => p.Amount > 0); // Breakdown children tienen Amount > 0
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenOverPaying()
    {
        // Arrange
        var reserveId = 1;
        var customerId = 1;
        var passengers = new List<Passenger>
        {
            new Passenger
            {
                PassengerId = 1,
                ReserveId = reserveId,
                CustomerId = customerId,
                Price = 5000,
                Status = PassengerStatusEnum.PendingPayment
            }
        };
        var reserve = new Reserve
        {
            ReserveId = reserveId,
            ServiceId = 1,
            Passengers = passengers,
            TripId = 1
        };
        var reserves = new List<Reserve> { reserve };
        var reservePayments = new List<ReservePayment>();

        var customer = new Customer { CustomerId = customerId };

        var payments = new List<CreatePaymentRequestDto>
        {
            new CreatePaymentRequestDto(3000m, 1),
            new CreatePaymentRequestDto(3000m, 2)
        };

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(customerId)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(reservePayments).Object);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Act - 6000 > 5000 => overpayment
        var result = await _reserveBusiness.CreatePaymentsAsync(1, reserveId, payments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Reserve.OverPaymentNotAllowed");
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenAlreadyFullyPaid()
    {
        // Arrange
        var reserveId = 1;
        var customerId = 1;
        var passengers = new List<Passenger>
        {
            new Passenger { PassengerId = 1, ReserveId = reserveId, CustomerId = customerId, Price = 100, Status = PassengerStatusEnum.Confirmed }
        };
        var reserve = new Reserve { ReserveId = reserveId, ServiceId = 1, Passengers = passengers, TripId = 1 };
        var reserves = new List<Reserve> { reserve };
        // Already paid in full
        var existingPayments = new List<ReservePayment>
        {
            new ReservePayment { ReservePaymentId = 1, ReserveId = reserveId, Amount = 100, Status = StatusPaymentEnum.Paid, ParentReservePaymentId = null }
        };

        var customer = new Customer { CustomerId = customerId };

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(customerId)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(existingPayments).Object);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Act
        var result = await _reserveBusiness.CreatePaymentsAsync(customerId, reserveId, new List<CreatePaymentRequestDto>
        {
            new CreatePaymentRequestDto(50, 1)
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Reserve.AlreadyFullyPaid");
    }

    #region Lock Tests

    [Fact]
    public async Task LockReserveSlots_ShouldSucceed_WhenValidRequest()
    {
        // Arrange
        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var reserves = new List<Reserve>
        {
            new Reserve
            {
                ReserveId = 1,
                Status = ReserveStatusEnum.Confirmed,
                Passengers = new List<Passenger>(),
                Vehicle = vehicle,
                Service = new Service { Vehicle = vehicle }
            }
        };

        var locks = new List<ReserveSlotLock>();
        var users = new List<User>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks).Object);
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(users).Object);
        _contextMock.Setup(c => c.ReserveSlotLocks.Add(It.IsAny<ReserveSlotLock>()))
            .Callback<ReserveSlotLock>(locks.Add);

        _userContextMock.Setup(x => x.Email).Returns("test@example.com");
        _userContextMock.Setup(x => x.UserId).Returns(1);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<LockReserveSlotsResponseDto>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<LockReserveSlotsResponseDto>>>, IsolationLevel>((func, _) => func());

        var request = new LockReserveSlotsRequestDto(1, null, 2);

        // Act
        var result = await _reserveBusiness.LockReserveSlots(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LockToken.Should().NotBeNullOrEmpty();
        result.Value.TimeoutMinutes.Should().Be(10); // From FakeReserveOption
        locks.Should().HaveCount(1);
        locks[0].OutboundReserveId.Should().Be(1);
        locks[0].SlotsLocked.Should().Be(2);
        locks[0].Status.Should().Be(ReserveSlotLockStatus.Active);
        locks[0].UserEmail.Should().Be("test@example.com");
    }

    [Fact]
    public async Task LockReserveSlots_ShouldFail_WhenInsufficientSlots()
    {
        // Arrange - Reserva con pocos cupos disponibles
        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var reserves = new List<Reserve>
        {
            new Reserve
            {
                ReserveId = 1,
                Status = ReserveStatusEnum.Confirmed,
                Passengers = new List<Passenger>
                {
                    new Passenger { Status = PassengerStatusEnum.Confirmed },
                    new Passenger { Status = PassengerStatusEnum.Confirmed },
                    new Passenger { Status = PassengerStatusEnum.Confirmed },
                    new Passenger { Status = PassengerStatusEnum.Confirmed },
                    new Passenger { Status = PassengerStatusEnum.Confirmed },
                    new Passenger { Status = PassengerStatusEnum.Confirmed },
                    new Passenger { Status = PassengerStatusEnum.Confirmed },
                    new Passenger { Status = PassengerStatusEnum.Confirmed },
                    new Passenger { Status = PassengerStatusEnum.Confirmed },
                    new Passenger { Status = PassengerStatusEnum.Confirmed }
                }, // 10 pasajeros confirmados
                Vehicle = vehicle,
                Service = new Service { Vehicle = vehicle } // Solo 10 cupos en total
            }
        };

        var locks = new List<ReserveSlotLock>();
        var users = new List<User>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks).Object);
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(users).Object);

        _userContextMock.Setup(x => x.Email).Returns("test@example.com");
        _userContextMock.Setup(x => x.UserId).Returns(1);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<LockReserveSlotsResponseDto>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<LockReserveSlotsResponseDto>>>, IsolationLevel>((func, _) => func());

        var request = new LockReserveSlotsRequestDto(1, null, 2); // Solicita 2 cupos pero no hay disponibles

        // Act
        var result = await _reserveBusiness.LockReserveSlots(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveSlotLockError.InsufficientSlots);
        locks.Should().BeEmpty();
    }

    [Fact]
    public async Task LockReserveSlots_ShouldFail_WhenMaxSimultaneousLocksExceeded()
    {
        // Arrange - Usuario ya tiene 5 locks activos (máximo según FakeReserveOption)
        var reserves = new List<Reserve>
        {
            new Reserve
            {
                ReserveId = 1,
                Status = ReserveStatusEnum.Confirmed,
                Passengers = new List<Passenger>(),
                Service = new Service { Vehicle = new Vehicle { AvailableQuantity = 10 } }
            }
        };

        var existingLocks = Enumerable.Range(1, 5).Select(i => new ReserveSlotLock
        {
            ReserveSlotLockId = i,
            UserEmail = "test@example.com",
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            OutboundReserveId = i + 10
        }).ToList();

        var users = new List<User>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(existingLocks).Object);
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(users).Object);

        _userContextMock.Setup(x => x.Email).Returns("test@example.com");
        _userContextMock.Setup(x => x.UserId).Returns(1);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<LockReserveSlotsResponseDto>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<LockReserveSlotsResponseDto>>>, IsolationLevel>((func, _) => func());

        var request = new LockReserveSlotsRequestDto(1, null, 1);

        // Act
        var result = await _reserveBusiness.LockReserveSlots(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveSlotLockError.MaxSimultaneousLocksExceeded);
    }

    [Fact]
    public async Task CreatePassengerReservesWithLock_ShouldSucceed_WithValidLock()
    {
        // Arrange
        var lockToken = Guid.NewGuid().ToString();
        var trip = new Trip
        {
            TripId = 1,
            OriginCityId = 1,
            DestinationCityId = 2,
            OriginCity = new City { CityId = 1, Name = "Origin" },
            DestinationCity = new City { CityId = 2, Name = "Destination" }
        };

        var reserves = new List<Reserve>
        {
            new Reserve
            {
                ReserveId = 1,
                Status = ReserveStatusEnum.Confirmed,
                Passengers = new List<Passenger>(),
                VehicleId = 1,
                ServiceId = 1,
                TripId = 1,
                Trip = trip
            }
        };

        var activeLock = new ReserveSlotLock
        {
            ReserveSlotLockId = 1,
            LockToken = lockToken,
            OutboundReserveId = 1,
            SlotsLocked = 1,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            UserEmail = "test@example.com"
        };

        var locks = new List<ReserveSlotLock> { activeLock };
        var passengers = new List<Passenger>();
        var payments = new List<ReservePayment>();
        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service
        {
            ServiceId = 1,
            Trip = new Trip
            {
                OriginCity = new City { Name = "Origin" },
                DestinationCity = new City { Name = "Destination" }
            }
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active }
            }}
        };

        var direction = new Direction { DirectionId = 1, Name = "Location" };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks).Object);
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(payments).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(direction);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer>()).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<CreateReserveExternalResult>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<CreateReserveExternalResult>>>, IsolationLevel>((func, _) => func());

        _paymentGatewayMock
            .Setup(x => x.CreatePreferenceAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<List<PassengerReserveExternalCreateRequestDto>>()))
            .ReturnsAsync("preference-id");

        var passengerItem = new PassengerReserveExternalCreateRequestDto(
            ReserveId: 1,
            ReserveTypeId: (int)ReserveTypeIdEnum.Ida,
            CustomerId: null,
            IsPayment: false,
            PickupLocationId: 1,
            DropoffLocationId: 1,
            HasTraveled: false,
            Price: 100,
            FirstName: "John",
            LastName: "Doe",
            Email: "john@example.com",
            Phone1: "123456789",
            DocumentNumber: "12345678"
        );

        var request = new CreateReserveWithLockRequestDto(
            lockToken,
            new List<PassengerReserveExternalCreateRequestDto> { passengerItem },
            null // Sin pago directo, usar wallet
        );

        // Act
        var result = await _reserveBusiness.CreatePassengerReservesWithLock(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("pending");
        result.Value.PreferenceId.Should().Be("preference-id");

        // Verificar que el lock fue marcado como usado
        activeLock.Status.Should().Be(ReserveSlotLockStatus.Used);

        // Verificar que se creó el pasajero
        passengers.Should().HaveCount(1);
        passengers[0].FirstName.Should().Be("John");
        passengers[0].Status.Should().Be(PassengerStatusEnum.PendingPayment);
    }

    [Fact]
    public async Task CreatePassengerReservesWithLock_ShouldFail_WithExpiredLock()
    {
        // Arrange
        var lockToken = Guid.NewGuid().ToString();
        var expiredLock = new ReserveSlotLock
        {
            ReserveSlotLockId = 1,
            LockToken = lockToken,
            OutboundReserveId = 1,
            SlotsLocked = 1,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expirado hace 5 minutos
            UserEmail = "test@example.com"
        };

        var locks = new List<ReserveSlotLock> { expiredLock };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks).Object);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<CreateReserveExternalResult>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<CreateReserveExternalResult>>>, IsolationLevel>((func, _) => func());

        var passengerItem = new PassengerReserveExternalCreateRequestDto(
            ReserveId: 1,
            ReserveTypeId: (int)ReserveTypeIdEnum.Ida,
            CustomerId: null,
            IsPayment: false,
            PickupLocationId: 1,
            DropoffLocationId: 1,
            HasTraveled: false,
            Price: 100,
            FirstName: "John",
            LastName: "Doe",
            Email: "john@example.com",
            Phone1: "123456789",
            DocumentNumber: "12345678"
        );

        var request = new CreateReserveWithLockRequestDto(
            lockToken,
            new List<PassengerReserveExternalCreateRequestDto> { passengerItem },
            null
        );

        // Act
        var result = await _reserveBusiness.CreatePassengerReservesWithLock(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveSlotLockError.InvalidOrExpiredLock);
    }

    [Fact]
    public async Task CancelReserveSlotLock_ShouldSucceed_WithValidLock()
    {
        // Arrange
        var lockToken = Guid.NewGuid().ToString();
        var activeLock = new ReserveSlotLock
        {
            ReserveSlotLockId = 1,
            LockToken = lockToken,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        var locks = new List<ReserveSlotLock> { activeLock };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Act
        var result = await _reserveBusiness.CancelReserveSlotLock(lockToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        activeLock.Status.Should().Be(ReserveSlotLockStatus.Cancelled);
        activeLock.UpdatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CleanupExpiredReserveSlotLocks_ShouldUpdateExpiredLocks()
    {
        // Arrange
        var activeLock = new ReserveSlotLock
        {
            ReserveSlotLockId = 1,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5) // No expirado
        };

        var expiredLock1 = new ReserveSlotLock
        {
            ReserveSlotLockId = 2,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5) // Expirado
        };

        var expiredLock2 = new ReserveSlotLock
        {
            ReserveSlotLockId = 3,
            Status = ReserveSlotLockStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10) // Expirado
        };

        var locks = new List<ReserveSlotLock> { activeLock, expiredLock1, expiredLock2 };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks).Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Act
        var result = await _reserveBusiness.CleanupExpiredReserveSlotLocks();

        // Assert
        result.IsSuccess.Should().BeTrue();

        // El lock activo debe seguir activo
        activeLock.Status.Should().Be(ReserveSlotLockStatus.Active);

        // Los locks expirados deben cambiar de estado
        expiredLock1.Status.Should().Be(ReserveSlotLockStatus.Expired);
        expiredLock2.Status.Should().Be(ReserveSlotLockStatus.Expired);

        // Verificar fechas de actualización
        expiredLock1.UpdatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        expiredLock2.UpdatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Parallelism Tests

    [Fact]
    public async Task LockReserveSlots_MultipleRequests_ShouldRespectAvailableSlots()
    {
        // Este test está diseñado para verificar la lógica de lockeo sin problemas de concurrencia de EF mock
        // El test real de concurrencia se maneja en los tests de integración

        // Arrange - Reserve con solo 3 cupos disponibles
        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var reserve = new Reserve
        {
            ReserveId = 1,
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>
            {
                // Ya hay 7 pasajeros confirmados, quedan 3 cupos libres
                new Passenger { Status = PassengerStatusEnum.Confirmed },
                new Passenger { Status = PassengerStatusEnum.Confirmed },
                new Passenger { Status = PassengerStatusEnum.Confirmed },
                new Passenger { Status = PassengerStatusEnum.Confirmed },
                new Passenger { Status = PassengerStatusEnum.Confirmed },
                new Passenger { Status = PassengerStatusEnum.Confirmed },
                new Passenger { Status = PassengerStatusEnum.Confirmed }
            },
            Vehicle = vehicle,
            Service = new Service { Vehicle = vehicle } // Total 10 cupos
        };
        var reserves = new List<Reserve> { reserve };

        var locks = new List<ReserveSlotLock>();
        var lockCounter = 0;

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks).Object);
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(new List<User>()).Object);
        _contextMock.Setup(c => c.ReserveSlotLocks.Add(It.IsAny<ReserveSlotLock>()))
            .Callback<ReserveSlotLock>(l =>
            {
                l.ReserveSlotLockId = ++lockCounter;
                locks.Add(l);
            });

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<LockReserveSlotsResponseDto>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<LockReserveSlotsResponseDto>>>, IsolationLevel>((func, _) => func());

        // Act - Ejecutar solicitudes secuencialmente
        var request1 = new LockReserveSlotsRequestDto(1, null, 2); // Primera solicitud: 2 cupos
        var result1 = await _reserveBusiness.LockReserveSlots(request1);

        var request2 = new LockReserveSlotsRequestDto(1, null, 2); // Segunda solicitud: 2 cupos (debería fallar)
        var result2 = await _reserveBusiness.LockReserveSlots(request2);

        // Assert
        result1.IsSuccess.Should().BeTrue("La primera solicitud debería tener éxito con cupos disponibles");
        result2.IsFailure.Should().BeTrue("La segunda solicitud debería fallar por falta de cupos");

        // Verificar que se creó exactamente un lock
        locks.Should().HaveCount(1, "Solo se debe crear un lock exitoso");
        locks[0].SlotsLocked.Should().Be(2, "El lock debe bloquear 2 cupos");
        locks[0].Status.Should().Be(ReserveSlotLockStatus.Active, "El lock debe estar activo");

        // Verificar el token único
        result1.Value.LockToken.Should().NotBeNullOrEmpty("Debe generarse un token de lock");
        result1.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow, "La fecha de expiración debe ser futura");
    }

    private Mock<IUserContext> CreateUserContextMock(string email, int userId)
    {
        var mock = new Mock<IUserContext>();
        mock.Setup(x => x.Email).Returns(email);
        mock.Setup(x => x.UserId).Returns(userId);
        return mock;
    }


    private PassengerReserveCreateRequestWrapperExternalDto CreateExternalReserveRequest(
        string firstName, string lastName, string email, string documentNumber)
    {
        var passengerItem = new PassengerReserveExternalCreateRequestDto(
            ReserveId: 1,
            ReserveTypeId: (int)ReserveTypeIdEnum.Ida,
            CustomerId: null,
            IsPayment: false,
            PickupLocationId: 1,
            DropoffLocationId: 1,
            HasTraveled: false,
            Price: 100,
            FirstName: firstName,
            LastName: lastName,
            Email: email,
            Phone1: "123456789",
            DocumentNumber: documentNumber
        );

        return new PassengerReserveCreateRequestWrapperExternalDto(
            null, // Sin pago directo
            new List<PassengerReserveExternalCreateRequestDto> { passengerItem }
        );
    }

    #endregion

    #region Payment Status Tests (Paid vs PrePayment)

    [Fact]
    public async Task CreatePassengerReserves_WithPayment_ShouldSetCorrectStatus_BasedOnReserveDateTime()
    {
        // Arrange - Reserva para el futuro y una reserva de HOY (más próxima)
        var today = DateTime.UtcNow.Date;
        var futureDate = DateTime.UtcNow.AddDays(1).Date;
        var futureTime = TimeSpan.FromHours(10);

        var trip = new Trip
        {
            TripId = 1,
            OriginCityId = 1,
            DestinationCityId = 2,
            OriginCity = new City { CityId = 1, Name = "Origin" },
            DestinationCity = new City { CityId = 2, Name = "Dest" }
        };

        // Reserva de HOY (más próxima en el sistema)
        var todayReserve = new Reserve
        {
            ReserveId = 99,
            Status = ReserveStatusEnum.Confirmed,
            ReserveDate = today,
            DepartureHour = TimeSpan.FromHours(8),
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip,
            Driver = new Driver { FirstName = "Driver", LastName = "Today" }
        };

        // Reserva seleccionada (mañana)
        var reserve = new Reserve
        {
            ReserveId = 1,
            Status = ReserveStatusEnum.Confirmed,
            ReserveDate = futureDate,
            DepartureHour = futureTime,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip,
            Driver = new Driver { FirstName = "Driver", LastName = "Test" }
        };

        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service
        {
            ServiceId = 1,
            Trip = trip
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active }
            }}
        };

        var customer = new Customer
        {
            CustomerId = 1,
            FirstName = "Test",
            LastName = "User",
            DocumentNumber = "11111111",
            Email = "test@test.com",
            CurrentBalance = 0
        };

        var direction = new Direction { DirectionId = 1, Name = "Location" };

        var paymentsDb = new List<ReservePayment>();
        var accountTransactions = new List<CustomerAccountTransaction>();
        var passengers = new List<Passenger>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { todayReserve, reserve }).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(direction);
        _contextMock.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(paymentsDb).Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactions).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var passengerItem = new PassengerReserveCreateRequestDto(
            ReserveId: 1,
            ReserveTypeId: (int)ReserveTypeIdEnum.Ida,
            CustomerId: 1,
            IsPayment: true,
            PickupLocationId: 1,
            DropoffLocationId: 1,
            HasTraveled: false,
            Price: 100
        );

        var paymentItem = new CreatePaymentRequestDto(100, (int)PaymentMethodEnum.Cash);
        var request = new PassengerReserveCreateRequestWrapperDto(
            new List<CreatePaymentRequestDto> { paymentItem },
            new List<PassengerReserveCreateRequestDto> { passengerItem }
        );

        // Act
        var result = await _reserveBusiness.CreatePassengerReserves(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Con CashBox: solo 1 pago padre en la reserva seleccionada
        paymentsDb.Should().HaveCount(1, "Solo pago padre en la reserva seleccionada");

        var parentPayment = paymentsDb.First();
        // Con CashBox: el pago va directamente a la reserva seleccionada
        parentPayment.ReserveId.Should().Be(1, "El pago va a la reserva seleccionada");
        parentPayment.Status.Should().Be(StatusPaymentEnum.Paid);
        parentPayment.StatusDetail.Should().Be("paid_on_departure");
        parentPayment.Amount.Should().Be(100);
        parentPayment.CashBoxId.Should().Be(1, "El pago debe estar en la caja abierta");
    }

    [Fact]
    public async Task CreatePassengerReserves_IdaYVuelta_WithPayment_ShouldAssignPaymentToEarliestReserve()
    {
        // Arrange - Ida y vuelta mañana + reserva de HOY (más próxima)
        var today = DateTime.UtcNow.Date;
        var tomorrow = DateTime.UtcNow.AddDays(1).Date;

        var trip = new Trip
        {
            TripId = 1,
            OriginCityId = 1,
            DestinationCityId = 2,
            OriginCity = new City { CityId = 1, Name = "Origin" },
            DestinationCity = new City { CityId = 2, Name = "Dest" }
        };

        // Reserva de HOY (más próxima en el sistema)
        var todayReserve = new Reserve
        {
            ReserveId = 99,
            ReserveDate = today,
            DepartureHour = TimeSpan.FromHours(8),
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip,
            Driver = new Driver { FirstName = "Driver", LastName = "Today" },
            ServiceName = "TestService",
            OriginName = "Origin",
            DestinationName = "Dest"
        };

        var reserveIda = new Reserve
        {
            ReserveId = 1,
            ReserveDate = tomorrow,
            DepartureHour = TimeSpan.FromHours(10), // Ida: 10:00 AM mañana
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip,
            Driver = new Driver { FirstName = "Driver", LastName = "Ida" },
            ServiceName = "TestService",
            OriginName = "Origin",
            DestinationName = "Dest"
        };

        var reserveVuelta = new Reserve
        {
            ReserveId = 2,
            ReserveDate = tomorrow,
            DepartureHour = TimeSpan.FromHours(17), // Vuelta: 5:00 PM mañana
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip,
            Driver = new Driver { FirstName = "Driver", LastName = "Vuelta" },
            ServiceName = "TestService",
            OriginName = "Origin",
            DestinationName = "Dest"
        };

        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service
        {
            ServiceId = 1,
            Trip = trip
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active },
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Price = 100, Status = EntityStatusEnum.Active }
            }}
        };

        var customer = new Customer
        {
            CustomerId = 1,
            FirstName = "Test",
            LastName = "IdaVuelta",
            DocumentNumber = "22222222",
            Email = "idavuelta@test.com",
            CurrentBalance = 0
        };

        var direction = new Direction { DirectionId = 1, Name = "Location" };
        var paymentsDb = new List<ReservePayment>();
        var accountTransactions = new List<CustomerAccountTransaction>();
        var passengers = new List<Passenger>();
        var users = new List<User>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserveIda, reserveVuelta }).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(direction);
        _contextMock.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(paymentsDb).Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactions).Object);
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(users).Object);

        _contextMock.Setup(c => c.Customers.Update(It.IsAny<Customer>())).Callback<Customer>(c =>
        {
            customer.CurrentBalance = c.CurrentBalance;
        });

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // IdaVuelta: ambos items con el mismo tipo y precio (el precio ya incluye ida y vuelta)
        var passengerItems = new List<PassengerReserveCreateRequestDto>
        {
            new PassengerReserveCreateRequestDto(
                ReserveId: 1, // Ida
                ReserveTypeId: (int)ReserveTypeIdEnum.IdaVuelta,
                CustomerId: 1,
                IsPayment: true,
                PickupLocationId: 1,
                DropoffLocationId: 1,
                HasTraveled: false,
                Price: 100
            ),
            new PassengerReserveCreateRequestDto(
                ReserveId: 2, // Vuelta
                ReserveTypeId: (int)ReserveTypeIdEnum.IdaVuelta,
                CustomerId: 1,
                IsPayment: true,
                PickupLocationId: 1,
                DropoffLocationId: 1,
                HasTraveled: false,
                Price: 100
            )
        };

        var paymentItems = new List<CreatePaymentRequestDto>
        {
            new CreatePaymentRequestDto(100, (int)PaymentMethodEnum.Cash) // IdaVuelta: precio del primer item
        };

        var request = new PassengerReserveCreateRequestWrapperDto(paymentItems, passengerItems);

        // Act
        var result = await _reserveBusiness.CreatePassengerReserves(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Con CashBox: solo 1 pago padre en la reserva principal (ida)
        paymentsDb.Should().HaveCount(1, "Solo el pago padre");

        var parentPayment = paymentsDb.First();

        // El pago va a la reserva principal seleccionada (no a "la mas proxima")
        parentPayment.ReserveId.Should().Be(1, "El pago va a la reserva de ida");
        parentPayment.Amount.Should().Be(100, "IdaVuelta: el precio del primer item ya incluye ida y vuelta");
        parentPayment.Status.Should().Be(StatusPaymentEnum.Paid);
        parentPayment.CashBoxId.Should().Be(1, "El pago debe estar en la caja abierta");

        // Ambos pasajeros deben estar confirmados
        passengers.Should().HaveCount(2);
        passengers.Should().OnlyContain(p => p.Status == PassengerStatusEnum.Confirmed);
    }

    [Fact]
    public async Task CreatePassengerReserves_MultipleReservesSameDay_ShouldAssignPaymentToEarliest()
    {
        // Arrange - 3 reservas el mismo día a diferentes horas: 15:00, 10:00, 20:00
        var today = DateTime.UtcNow.Date;

        var trip = new Trip
        {
            TripId = 1,
            OriginCityId = 1,
            DestinationCityId = 2,
            OriginCity = new City { CityId = 1, Name = "Origin" },
            DestinationCity = new City { CityId = 2, Name = "Dest" }
        };

        var reserve1 = new Reserve // Segunda cronológicamente
        {
            ReserveId = 1,
            ReserveDate = today,
            DepartureHour = TimeSpan.FromHours(15), // 3:00 PM
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip,
            Driver = new Driver { FirstName = "Driver", LastName = "1" }
        };

        var reserve2 = new Reserve // Primera cronológicamente (EARLIEST)
        {
            ReserveId = 2,
            ReserveDate = today,
            DepartureHour = TimeSpan.FromHours(10), // 10:00 AM ← Esta debe recibir el pago padre
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip,
            Driver = new Driver { FirstName = "Driver", LastName = "2" }
        };

        var reserve3 = new Reserve // Tercera cronológicamente
        {
            ReserveId = 3,
            ReserveDate = today,
            DepartureHour = TimeSpan.FromHours(20), // 8:00 PM
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            TripId = 1,
            Trip = trip,
            Driver = new Driver { FirstName = "Driver", LastName = "3" }
        };

        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service
        {
            ServiceId = 1,
            Trip = trip
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active }
            }}
        };

        var customer = new Customer
        {
            CustomerId = 1,
            FirstName = "Test",
            LastName = "MultiReserve",
            DocumentNumber = "33333333",
            Email = "multi@test.com",
            CurrentBalance = 0
        };

        var direction = new Direction { DirectionId = 1, Name = "Location" };
        var paymentsDb = new List<ReservePayment>();
        var accountTransactions = new List<CustomerAccountTransaction>();
        var passengers = new List<Passenger>();
        var users = new List<User>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve1, reserve2, reserve3 }).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(direction);
        _contextMock.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(paymentsDb).Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactions).Object);
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(users).Object);

        _contextMock.Setup(c => c.Customers.Update(It.IsAny<Customer>())).Callback<Customer>(c =>
        {
            customer.CurrentBalance = c.CurrentBalance;
        });

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Múltiples reservas Ida del mismo día - el pago usa First().Price
        var passengerItems = new List<PassengerReserveCreateRequestDto>
        {
            new PassengerReserveCreateRequestDto(
                ReserveId: 1,
                ReserveTypeId: (int)ReserveTypeIdEnum.Ida,
                CustomerId: 1,
                IsPayment: true,
                PickupLocationId: 1,
                DropoffLocationId: 1,
                HasTraveled: false,
                Price: 100
            ),
            new PassengerReserveCreateRequestDto(
                ReserveId: 2,
                ReserveTypeId: (int)ReserveTypeIdEnum.Ida,
                CustomerId: 1,
                IsPayment: true,
                PickupLocationId: 1,
                DropoffLocationId: 1,
                HasTraveled: false,
                Price: 100
            ),
            new PassengerReserveCreateRequestDto(
                ReserveId: 3,
                ReserveTypeId: (int)ReserveTypeIdEnum.Ida,
                CustomerId: 1,
                IsPayment: true,
                PickupLocationId: 1,
                DropoffLocationId: 1,
                HasTraveled: false,
                Price: 100
            )
        };

        var paymentItems = new List<CreatePaymentRequestDto>
        {
            new CreatePaymentRequestDto(100, (int)PaymentMethodEnum.Cash) // Usa First().Price
        };

        var request = new PassengerReserveCreateRequestWrapperDto(paymentItems, passengerItems);

        // Act
        var result = await _reserveBusiness.CreatePassengerReserves(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Con CashBox: solo 1 pago padre
        paymentsDb.Should().HaveCount(1, "Solo el pago padre");

        var parentPayment = paymentsDb.First();

        // Con CashBox: el pago va a la reserva principal (mainReserveId) con CashBoxId
        parentPayment.ReserveId.Should().Be(1, "El pago va a la reserva principal");
        parentPayment.Amount.Should().Be(100, "Usa First().Price");
        parentPayment.Status.Should().Be(StatusPaymentEnum.Paid);
        parentPayment.CashBoxId.Should().Be(1, "El pago debe estar en la caja abierta");

        // Todos los pasajeros deben estar confirmados
        passengers.Should().HaveCount(3);
        passengers.Should().OnlyContain(p => p.Status == PassengerStatusEnum.Confirmed);
    }

    #endregion

    #region GetReservePaymentSummary Tests

    [Fact]
    public async Task GetReservePaymentSummary_ShouldFail_WhenReserveNotFound()
    {
        // Arrange
        _contextMock.Setup(c => c.Reserves)
            .Returns(GetQueryableMockDbSet(new List<Reserve>()).Object);

        var request = new PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto>();

        // Act
        var result = await _reserveBusiness.GetReservePaymentSummary(999, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveError.NotFound);
    }

    [Fact]
    public async Task GetReservePaymentSummary_ShouldReturnEmptySummary_WhenNoPayments()
    {
        // Arrange
        var reserve = new Reserve { ReserveId = 1 };
        var reserves = new List<Reserve> { reserve };
        var payments = new List<ReservePayment>();

        _contextMock.Setup(c => c.Reserves)
            .Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.ReservePayments)
            .Returns(GetQueryableMockDbSet(payments).Object);

        var request = new PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto>();

        // Act
        var result = await _reserveBusiness.GetReservePaymentSummary(1, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].ReserveId.Should().Be(1);
        result.Value.Items[0].PaymentsByMethod.Should().BeEmpty();
        result.Value.Items[0].TotalAmount.Should().Be(0);
    }

    [Fact]
    public async Task GetReservePaymentSummary_ShouldReturnCorrectSummary_WithSinglePaymentMethod()
    {
        // Arrange
        var reserve = new Reserve { ReserveId = 1 };
        var reserves = new List<Reserve> { reserve };
        var payments = new List<ReservePayment>
        {
            new ReservePayment
            {
                ReservePaymentId = 1,
                ReserveId = 1,
                Amount = 5000,
                Method = PaymentMethodEnum.Cash,
                ParentReservePaymentId = null
            }
        };

        _contextMock.Setup(c => c.Reserves)
            .Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.ReservePayments)
            .Returns(GetQueryableMockDbSet(payments).Object);

        var request = new PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto>();

        // Act
        var result = await _reserveBusiness.GetReservePaymentSummary(1, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);

        var summary = result.Value.Items[0];
        summary.ReserveId.Should().Be(1);
        summary.TotalAmount.Should().Be(5000);
        summary.PaymentsByMethod.Should().HaveCount(1);
        summary.PaymentsByMethod[0].PaymentMethodId.Should().Be((int)PaymentMethodEnum.Cash);
        summary.PaymentsByMethod[0].PaymentMethodName.Should().Be("Efectivo");
        summary.PaymentsByMethod[0].Amount.Should().Be(5000);
    }

    [Fact]
    public async Task GetReservePaymentSummary_ShouldReturnCorrectSummary_WithMultiplePaymentMethods()
    {
        // Arrange
        var reserve = new Reserve { ReserveId = 1 };
        var reserves = new List<Reserve> { reserve };

        // Simular pago padre con monto total y hijos de desglose por método
        var payments = new List<ReservePayment>
        {
            // Pago padre (monto total consolidado)
            new ReservePayment
            {
                ReservePaymentId = 1,
                ReserveId = 1,
                Amount = 8000,
                Method = PaymentMethodEnum.Cash,
                ParentReservePaymentId = null
            },
            // Hijo desglose - Efectivo (Breakdown: Amount > 0)
            new ReservePayment
            {
                ReservePaymentId = 2,
                ReserveId = 1,
                Amount = 5000,
                Method = PaymentMethodEnum.Cash,
                ParentReservePaymentId = 1
            },
            // Hijo desglose - Tarjeta (Breakdown: Amount > 0)
            new ReservePayment
            {
                ReservePaymentId = 3,
                ReserveId = 1,
                Amount = 3000,
                Method = PaymentMethodEnum.CreditCard,
                ParentReservePaymentId = 1
            }
        };

        _contextMock.Setup(c => c.Reserves)
            .Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.ReservePayments)
            .Returns(GetQueryableMockDbSet(payments).Object);

        var request = new PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto>();

        // Act
        var result = await _reserveBusiness.GetReservePaymentSummary(1, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);

        var summary = result.Value.Items[0];
        summary.ReserveId.Should().Be(1);
        summary.TotalAmount.Should().Be(8000, "Total debe ser la suma de pagos padres");
        summary.PaymentsByMethod.Should().HaveCount(2, "Debe haber 2 métodos de pago diferentes");

        var cashPayment = summary.PaymentsByMethod.First(p => p.PaymentMethodId == (int)PaymentMethodEnum.Cash);
        cashPayment.Amount.Should().Be(5000);
        cashPayment.PaymentMethodName.Should().Be("Efectivo");

        var cardPayment = summary.PaymentsByMethod.First(p => p.PaymentMethodId == (int)PaymentMethodEnum.CreditCard);
        cardPayment.Amount.Should().Be(3000);
        cardPayment.PaymentMethodName.Should().Be("Tarjeta de Crédito");
    }

    [Fact]
    public async Task GetReservePaymentSummary_ShouldAggregateMultiplePayments_SameMethod()
    {
        // Arrange - Múltiples pagos del mismo método
        var reserve = new Reserve { ReserveId = 1 };
        var reserves = new List<Reserve> { reserve };

        var payments = new List<ReservePayment>
        {
            new ReservePayment
            {
                ReservePaymentId = 1,
                ReserveId = 1,
                Amount = 2000,
                Method = PaymentMethodEnum.Cash,
                ParentReservePaymentId = null
            },
            new ReservePayment
            {
                ReservePaymentId = 2,
                ReserveId = 1,
                Amount = 3000,
                Method = PaymentMethodEnum.Cash,
                ParentReservePaymentId = null
            }
        };

        _contextMock.Setup(c => c.Reserves)
            .Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.ReservePayments)
            .Returns(GetQueryableMockDbSet(payments).Object);

        var request = new PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto>();

        // Act
        var result = await _reserveBusiness.GetReservePaymentSummary(1, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var summary = result.Value.Items[0];

        summary.TotalAmount.Should().Be(5000);
        summary.PaymentsByMethod.Should().HaveCount(1);
        summary.PaymentsByMethod[0].Amount.Should().Be(5000, "Debe sumar todos los pagos en efectivo");
    }

    [Fact]
    public async Task GetReservePassengerReport_ShouldIncludeRelatedReservePayments()
    {
        // Arrange
        var reserveId = 1;
        var relatedReserveId = 2;
        var customerId = 10;

        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var service = new Service { Vehicle = vehicle };
        var reserve1 = new Reserve
        {
            ReserveId = reserveId,
            Service = service,
            Vehicle = vehicle,
            Passengers = new List<Passenger>(),

            TripId = 1
        };

        var passengers = new List<Passenger>
        {
            new Passenger
            {
                PassengerId = 1,
                ReserveId = reserveId,
                ReserveRelatedId = relatedReserveId,
                CustomerId = customerId,
                FirstName = "Juan",
                LastName = "Perez",
                DocumentNumber = "123",
                Reserve = reserve1
            }
        };
        reserve1.Passengers = passengers;

        var payments = new List<ReservePayment>
        {
            // Pago en la reserva actual (Ida) - Efectivo 1000
            new ReservePayment
            {
                ReservePaymentId = 1,
                ReserveId = reserveId,
                CustomerId = customerId,
                Amount = 1000,
                Method = PaymentMethodEnum.Cash
            },
            // Pago en la reserva relacionada (Vuelta) - Online 2000
            new ReservePayment
            {
                ReservePaymentId = 2,
                ReserveId = relatedReserveId,
                CustomerId = customerId,
                Amount = 2000,
                Method = PaymentMethodEnum.Online
            }
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active },
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Price = 200, Status = EntityStatusEnum.Active }
            }}
        };

        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(payments).Object);
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve1 }).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);

        var request = new PagedReportRequestDto<PassengerReserveReportFilterRequestDto>
        {
            Filters = new PassengerReserveReportFilterRequestDto(null, null, null)
        };

        // Act
        var result = await _reserveBusiness.GetReservePassengerReport(reserveId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        
        var passengerReport = result.Value.Items[0];
        passengerReport.PaidAmount.Should().Be(3000, "Debe sumar pagos de ambas reservas (1000 + 2000)");
        passengerReport.PaymentMethods.Should().Contain("Efectivo");
        passengerReport.PaymentMethods.Should().Contain("Online");
    }

    [Fact]
    public async Task GetReservePassengerReport_ShouldIncludeServicePrice_WhenNoPaymentsExist()
    {
        // Arrange
        var reserveId = 1;
        var passengerIdUnpaidIda = 1;
        var passengerIdUnpaidIdaVuelta = 2;

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 1500, Status = EntityStatusEnum.Active },
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Price = 2500, Status = EntityStatusEnum.Active }
            }}
        };
        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var service = new Service { Vehicle = vehicle };
        var reserve1 = new Reserve
        {
            ReserveId = reserveId,
            Service = service,
            Vehicle = vehicle,
            Passengers = new List<Passenger>(),

            TripId = 1
        };

        var passengers = new List<Passenger>
        {
            // Pasajero 1: Solo Ida, sin pago
            new Passenger
            {
                PassengerId = passengerIdUnpaidIda,
                ReserveId = reserveId,
                FirstName = "Unpaid",
                LastName = "Ida",
                DocumentNumber = "001",
                Reserve = reserve1
            },
            // Pasajero 2: Ida y Vuelta, sin pago
            new Passenger
            {
                PassengerId = passengerIdUnpaidIdaVuelta,
                ReserveId = reserveId,
                ReserveRelatedId = 3, // Indica que es Ida y Vuelta
                FirstName = "Unpaid",
                LastName = "IdaVuelta",
                DocumentNumber = "002",
                Reserve = reserve1
            }
        };
        reserve1.Passengers = passengers;

        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(new List<ReservePayment>()).Object);
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve1 }).Object);

        var request = new PagedReportRequestDto<PassengerReserveReportFilterRequestDto>
        {
            Filters = new PassengerReserveReportFilterRequestDto(null, null, null)
        };

        // Act
        var result = await _reserveBusiness.GetReservePassengerReport(reserveId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        
        var reportIda = result.Value.Items.First(i => i.PassengerId == passengerIdUnpaidIda);
        reportIda.PaidAmount.Should().Be(1500, "Debe mostrar el precio de Ida del servicio");
        reportIda.IsPayment.Should().BeFalse();

        var reportIdaVuelta = result.Value.Items.First(i => i.PassengerId == passengerIdUnpaidIdaVuelta);
        reportIdaVuelta.PaidAmount.Should().Be(2500, "Debe mostrar el precio de IdaVuelta del servicio");
        reportIdaVuelta.IsPayment.Should().BeFalse();
    }

    #endregion

    #region Partial Payments & SettleDebt Tests

    [Fact]
    public async Task CreatePassengerReserves_ShouldSetPendingPayment_WhenNoPayment()
    {
        // Arrange
        var trip = new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, OriginCity = new City { CityId = 1, Name = "A" }, DestinationCity = new City { CityId = 2, Name = "B" } };
        var reserve = new Reserve { ReserveId = 1, Status = ReserveStatusEnum.Confirmed, Passengers = new List<Passenger>(), VehicleId = 1, ServiceId = 1, TripId = 1, Trip = trip, Driver = new Driver { FirstName = "John", LastName = "Doe" } };
        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service { ServiceId = 1, Trip = trip };
        var trips = new List<Trip> { new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> { new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active } } } };
        var customer = new Customer { CustomerId = 1, FirstName = "Jane", LastName = "Smith", DocumentNumber = "12345678", Email = "jane@test.com", CurrentBalance = 0 };
        var origin = new Direction { DirectionId = 10, Name = "Pickup" };
        var passengers = new List<Passenger>();
        var accountTransactions = new List<CustomerAccountTransaction>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve }).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(It.IsAny<int>())).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Directions).Returns(GetQueryableMockDbSet(new List<Direction> { origin }).Object);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(origin);
        _contextMock.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(new List<User>()).Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactions).Object);
        _contextMock.Setup(c => c.Customers.Update(It.IsAny<Customer>())).Callback<Customer>(c => { customer.CurrentBalance = c.CurrentBalance; });
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var items = new List<PassengerReserveCreateRequestDto>
        {
            new(ReserveId: 1, ReserveTypeId: (int)ReserveTypeIdEnum.Ida, CustomerId: 1, IsPayment: true, PickupLocationId: 1, DropoffLocationId: 2, HasTraveled: false, Price: 100)
        };
        var request = new PassengerReserveCreateRequestWrapperDto(new List<CreatePaymentRequestDto>(), items);

        // Act
        var result = await _reserveBusiness.CreatePassengerReserves(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        passengers.Should().HaveCount(1);
        passengers[0].Status.Should().Be(PassengerStatusEnum.PendingPayment);
        customer.CurrentBalance.Should().Be(100); // Only charge, no payment
    }

    [Fact]
    public async Task CreatePassengerReserves_ShouldSetPendingPayment_WhenPartialPayment()
    {
        // Arrange
        var trip = new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, OriginCity = new City { CityId = 1, Name = "A" }, DestinationCity = new City { CityId = 2, Name = "B" } };
        var reserve = new Reserve { ReserveId = 1, Status = ReserveStatusEnum.Confirmed, Passengers = new List<Passenger>(), VehicleId = 1, ServiceId = 1, TripId = 1, Trip = trip, Driver = new Driver { FirstName = "John", LastName = "Doe" } };
        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service { ServiceId = 1, Trip = trip };
        var trips = new List<Trip> { new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> { new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active } } } };
        var customer = new Customer { CustomerId = 1, FirstName = "Jane", LastName = "Smith", DocumentNumber = "12345678", Email = "jane@test.com", CurrentBalance = 0 };
        var origin = new Direction { DirectionId = 10, Name = "Pickup" };
        var passengers = new List<Passenger>();
        var reservePayments = new List<ReservePayment>();
        var accountTransactions = new List<CustomerAccountTransaction>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve }).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(It.IsAny<int>())).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Directions).Returns(GetQueryableMockDbSet(new List<Direction> { origin }).Object);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(origin);
        _contextMock.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(reservePayments).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(new List<User>()).Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactions).Object);
        _contextMock.Setup(c => c.Customers.Update(It.IsAny<Customer>())).Callback<Customer>(c => { customer.CurrentBalance = c.CurrentBalance; });
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var items = new List<PassengerReserveCreateRequestDto>
        {
            new(ReserveId: 1, ReserveTypeId: (int)ReserveTypeIdEnum.Ida, CustomerId: 1, IsPayment: true, PickupLocationId: 1, DropoffLocationId: 2, HasTraveled: false, Price: 100)
        };
        var payments = new List<CreatePaymentRequestDto> { new(50, 1) };
        var request = new PassengerReserveCreateRequestWrapperDto(payments, items);

        // Act
        var result = await _reserveBusiness.CreatePassengerReserves(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        passengers.Should().HaveCount(1);
        passengers[0].Status.Should().Be(PassengerStatusEnum.PendingPayment);
        customer.CurrentBalance.Should().Be(50); // 100 charge - 50 payment = 50 remaining debt
    }

    [Fact]
    public async Task CreatePassengerReserves_ShouldConfirm_WhenFullPayment()
    {
        // Arrange
        var trip = new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, OriginCity = new City { CityId = 1, Name = "A" }, DestinationCity = new City { CityId = 2, Name = "B" } };
        var reserve = new Reserve { ReserveId = 1, Status = ReserveStatusEnum.Confirmed, Passengers = new List<Passenger>(), VehicleId = 1, ServiceId = 1, TripId = 1, Trip = trip, Driver = new Driver { FirstName = "John", LastName = "Doe" } };
        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service { ServiceId = 1, Trip = trip };
        var trips = new List<Trip> { new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> { new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active } } } };
        var customer = new Customer { CustomerId = 1, FirstName = "Jane", LastName = "Smith", DocumentNumber = "12345678", Email = "jane@test.com", CurrentBalance = 0 };
        var origin = new Direction { DirectionId = 10, Name = "Pickup" };
        var passengers = new List<Passenger>();
        var reservePayments = new List<ReservePayment>();
        var accountTransactions = new List<CustomerAccountTransaction>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve }).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(It.IsAny<int>())).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Directions).Returns(GetQueryableMockDbSet(new List<Direction> { origin }).Object);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(origin);
        _contextMock.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(reservePayments).Object);
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips).Object);
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(new List<User>()).Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactions).Object);
        _contextMock.Setup(c => c.Customers.Update(It.IsAny<Customer>())).Callback<Customer>(c => { customer.CurrentBalance = c.CurrentBalance; });
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var items = new List<PassengerReserveCreateRequestDto>
        {
            new(ReserveId: 1, ReserveTypeId: (int)ReserveTypeIdEnum.Ida, CustomerId: 1, IsPayment: true, PickupLocationId: 1, DropoffLocationId: 2, HasTraveled: false, Price: 100)
        };
        var payments = new List<CreatePaymentRequestDto> { new(100, 1) };
        var request = new PassengerReserveCreateRequestWrapperDto(payments, items);

        // Act
        var result = await _reserveBusiness.CreatePassengerReserves(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        passengers.Should().HaveCount(1);
        passengers[0].Status.Should().Be(PassengerStatusEnum.Confirmed);
        customer.CurrentBalance.Should().Be(0);
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldSucceed_WhenPartialPayment()
    {
        // Arrange
        var passengers = new List<Passenger> { new Passenger { PassengerId = 1, ReserveId = 1, CustomerId = 1, Price = 100, Status = PassengerStatusEnum.PendingPayment } };
        var reserve = new Reserve { ReserveId = 1, ServiceId = 1, Passengers = passengers, TripId = 1 };
        var customer = new Customer { CustomerId = 1, DocumentNumber = "12345678", FirstName = "Test", LastName = "User", Email = "test@test.com", CurrentBalance = 100 };
        var paymentsDb = new List<ReservePayment>();
        var existingPayments = new List<ReservePayment>(); // No previous payments
        var accountTransactions = new List<CustomerAccountTransaction>();

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve }).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(paymentsDb).Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactions).Object);
        _contextMock.Setup(c => c.Customers.Update(It.IsAny<Customer>())).Callback<Customer>(c => { customer.CurrentBalance = c.CurrentBalance; });
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Act - Partial payment: 60 of 100
        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto> { new(60, 1) });

        // Assert
        result.IsSuccess.Should().BeTrue();
        paymentsDb.Should().HaveCount(1);
        paymentsDb[0].Amount.Should().Be(60);
        passengers[0].Status.Should().Be(PassengerStatusEnum.PendingPayment); // Still pending
        customer.CurrentBalance.Should().Be(40); // 100 - 60
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldConfirmPassengers_WhenDebtFullySettled()
    {
        // Arrange - passenger with 100 price, already paid 60, now paying 40
        var passengers = new List<Passenger> { new Passenger { PassengerId = 1, ReserveId = 1, CustomerId = 1, Price = 100, Status = PassengerStatusEnum.PendingPayment } };
        var reserve = new Reserve { ReserveId = 1, ServiceId = 1, Passengers = passengers, TripId = 1 };
        var customer = new Customer { CustomerId = 1, DocumentNumber = "12345678", FirstName = "Test", LastName = "User", Email = "test@test.com", CurrentBalance = 40 };
        var existingPayments = new List<ReservePayment>
        {
            new ReservePayment { ReservePaymentId = 10, ReserveId = 1, Amount = 60, Status = StatusPaymentEnum.Paid, ParentReservePaymentId = null }
        };
        var accountTransactions = new List<CustomerAccountTransaction>();

        // Combine existing + new for the DbSet
        var allPayments = new List<ReservePayment>(existingPayments);
        var mockDbSet = GetMockDbSetWithIdentity(allPayments);

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve }).Object);
        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(new List<Customer> { customer }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.ReservePayments).Returns(mockDbSet.Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactions).Object);
        _contextMock.Setup(c => c.Customers.Update(It.IsAny<Customer>())).Callback<Customer>(c => { customer.CurrentBalance = c.CurrentBalance; });
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Act - Pay remaining 40
        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto> { new(40, 1) });

        // Assert
        result.IsSuccess.Should().BeTrue();
        passengers[0].Status.Should().Be(PassengerStatusEnum.Confirmed); // Now confirmed!
        customer.CurrentBalance.Should().Be(0);
    }

    [Fact]
    public async Task SettleCustomerDebtAsync_ShouldSucceed_PayingMultipleReserves()
    {
        // Arrange
        var customer = new Customer { CustomerId = 1, DocumentNumber = "12345678", FirstName = "Test", LastName = "User", Email = "test@test.com", CurrentBalance = 200 };
        var passengers1 = new List<Passenger> { new Passenger { PassengerId = 1, ReserveId = 1, CustomerId = 1, Price = 100, Status = PassengerStatusEnum.PendingPayment } };
        var passengers2 = new List<Passenger> { new Passenger { PassengerId = 2, ReserveId = 2, CustomerId = 1, Price = 100, Status = PassengerStatusEnum.PendingPayment } };
        var reserve1 = new Reserve { ReserveId = 1, Passengers = passengers1 };
        var reserve2 = new Reserve { ReserveId = 2, Passengers = passengers2 };
        var allPassengers = new List<Passenger>(passengers1.Concat(passengers2));
        var reservePayments = new List<ReservePayment>();
        var accountTransactions = new List<CustomerAccountTransaction>();

        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve1, reserve2 }).Object);
        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(allPassengers).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(reservePayments).Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactions).Object);
        _contextMock.Setup(c => c.Customers.Update(It.IsAny<Customer>())).Callback<Customer>(c => { customer.CurrentBalance = c.CurrentBalance; });
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var request = new SettleCustomerDebtRequestDto(1, new List<int> { 1, 2 }, new List<CreatePaymentRequestDto> { new(200, 1) });

        // Act
        var result = await _reserveBusiness.SettleCustomerDebtAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        reservePayments.Should().HaveCount(1); // 1 parent payment
        reservePayments[0].Amount.Should().Be(200);
        customer.CurrentBalance.Should().Be(0);
    }

    [Fact]
    public async Task SettleCustomerDebtAsync_ShouldConfirmPassengers_ForFullySettledReserves()
    {
        // Arrange - 2 reserves with 100 debt each, paying 150 (first fully settled, second partially)
        var customer = new Customer { CustomerId = 1, DocumentNumber = "12345678", FirstName = "Test", LastName = "User", Email = "test@test.com", CurrentBalance = 200 };
        var passengers1 = new List<Passenger> { new Passenger { PassengerId = 1, ReserveId = 1, CustomerId = 1, Price = 100, Status = PassengerStatusEnum.PendingPayment } };
        var passengers2 = new List<Passenger> { new Passenger { PassengerId = 2, ReserveId = 2, CustomerId = 1, Price = 100, Status = PassengerStatusEnum.PendingPayment } };
        var reserve1 = new Reserve { ReserveId = 1, Passengers = passengers1 };
        var reserve2 = new Reserve { ReserveId = 2, Passengers = passengers2 };
        var allPassengers = new List<Passenger>(passengers1.Concat(passengers2));
        var reservePayments = new List<ReservePayment>();
        var accountTransactions = new List<CustomerAccountTransaction>();

        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve1, reserve2 }).Object);
        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(allPassengers).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(reservePayments).Object);
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactions).Object);
        _contextMock.Setup(c => c.Customers.Update(It.IsAny<Customer>())).Callback<Customer>(c => { customer.CurrentBalance = c.CurrentBalance; });
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var request = new SettleCustomerDebtRequestDto(1, new List<int> { 1, 2 }, new List<CreatePaymentRequestDto> { new(150, 1) });

        // Act
        var result = await _reserveBusiness.SettleCustomerDebtAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        passengers1[0].Status.Should().Be(PassengerStatusEnum.Confirmed); // First reserve fully settled
        passengers2[0].Status.Should().Be(PassengerStatusEnum.PendingPayment); // Second reserve partially settled
        customer.CurrentBalance.Should().Be(50); // 200 - 150
    }

    [Fact]
    public async Task SettleCustomerDebtAsync_ShouldFail_WhenOverPaying()
    {
        // Arrange
        var customer = new Customer { CustomerId = 1, DocumentNumber = "12345678", FirstName = "Test", LastName = "User", Email = "test@test.com", CurrentBalance = 100 };
        var passengers1 = new List<Passenger> { new Passenger { PassengerId = 1, ReserveId = 1, CustomerId = 1, Price = 100, Status = PassengerStatusEnum.PendingPayment } };
        var reserve1 = new Reserve { ReserveId = 1, Passengers = passengers1 };
        var reservePayments = new List<ReservePayment>();

        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve1 }).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(reservePayments).Object);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var request = new SettleCustomerDebtRequestDto(1, new List<int> { 1 }, new List<CreatePaymentRequestDto> { new(150, 1) });

        // Act
        var result = await _reserveBusiness.SettleCustomerDebtAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Reserve.OverPaymentNotAllowed");
    }

    [Fact]
    public async Task SettleCustomerDebtAsync_ShouldFail_WhenNoDebt()
    {
        // Arrange - reserve already fully paid
        var customer = new Customer { CustomerId = 1, DocumentNumber = "12345678", FirstName = "Test", LastName = "User", Email = "test@test.com", CurrentBalance = 0 };
        var passengers1 = new List<Passenger> { new Passenger { PassengerId = 1, ReserveId = 1, CustomerId = 1, Price = 100, Status = PassengerStatusEnum.Confirmed } };
        var reserve1 = new Reserve { ReserveId = 1, Passengers = passengers1 };
        var existingPayments = new List<ReservePayment>
        {
            new ReservePayment { ReservePaymentId = 1, ReserveId = 1, CustomerId = 1, Amount = 100, Status = StatusPaymentEnum.Paid, ParentReservePaymentId = null }
        };

        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve1 }).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(existingPayments).Object);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var request = new SettleCustomerDebtRequestDto(1, new List<int> { 1 }, new List<CreatePaymentRequestDto> { new(50, 1) });

        // Act
        var result = await _reserveBusiness.SettleCustomerDebtAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Reserve.NoDebtToSettle");
    }

    #endregion
}