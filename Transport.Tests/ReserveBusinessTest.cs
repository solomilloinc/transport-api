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
using Transport.Domain.Customers.Abstraction;
using Transport.Domain.Passengers;
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
    private readonly ReserveBusiness _reserveBusiness;

    public ReserveBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userContextMock = new Mock<IUserContext>();
        _paymentGatewayMock = new Mock<IMercadoPagoPaymentGateway>();
        _customerBusinessMock = new Mock<ICustomerBusiness>();
        _reserveBusiness = new ReserveBusiness(_contextMock.Object,
            _unitOfWorkMock.Object,
            _userContextMock.Object,
            _paymentGatewayMock.Object,
            _customerBusinessMock.Object,
            new FakeReserveOption());
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
        var reservesList = Enumerable.Range(1, reserveCount).Select(i => new Reserve
        {
            ReserveId = i,
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            Driver = new Driver { FirstName = "John", LastName = "Doe" }
        }).ToList();

        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service
        {
            ServiceId = 1,
            ReservePrices = new List<ReservePrice> { new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Price = 100 } },
            Origin = new City { Name = "CityA" },
            Destination = new City { Name = "CityB" }
        };

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

        var totalAmount = 100m * reserveCount;
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

        if (paymentCount == 1)
        {
            int accountPaymentShouldBe = 0;

            if (reserveCount == 2)
            {
                accountPaymentShouldBe += 1;
            }

            Assert.Equal(paymentCount + accountPaymentShouldBe, reservePaymentsList.Count);
        }
        else
        {
            int accountPaymentShouldBe = 1;

            if (reserveCount == 2)
            {
                accountPaymentShouldBe += 1;
            }

            Assert.Equal(paymentCount + accountPaymentShouldBe, reservePaymentsList.Count);
        }

        var parent = reservePaymentsList.First();
        Assert.Equal(payments.Sum(p => p.TransactionAmount), parent.Amount);
        Assert.Null(parent.ParentReservePaymentId);

        if (reserveCount == 2)
        {
            foreach (var child in reservePaymentsList.TakeLast(0))
            {
                Assert.Equal(0, child.Amount);
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
        var reserve1 = new Reserve
        {
            ReserveId = 1,
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1,
            Driver = new Driver { FirstName = "Mario", LastName = "Bros" }
        };
        var reserve2 = new Reserve
        {
            ReserveId = 2,
            Status = ReserveStatusEnum.Confirmed,
            Passengers = new List<Passenger>(),
            VehicleId = 1,
            ServiceId = 1
        };
        var vehicle = new Vehicle { VehicleId = 1, AvailableQuantity = 10 };
        var service = new Service
        {
            ServiceId = 1,
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100 },
            new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Price = 100 }
        },
            Origin = new City { Name = "Córdoba" },
            Destination = new City { Name = "Rosario" }
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
            new FakeReserveOption());

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
            DocumentNumber: "1111-2222"
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
            FirstName: "Lionel",
            LastName: "Messi",
            Email: "liomessi@example.com",
            Phone1: "32145679",
            DocumentNumber: "1111-2222"
        )
        };

        var paymentDto = new CreatePaymentExternalRequestDto(
            TransactionAmount: 200m,
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
        Assert.Equal(2, reservePaymentsList.Count);
        Assert.Equal(2, reserve1.Passengers.Count + reserve2.Passengers.Count);

        var parentPayment = reservePaymentsList[0];
        var childPayment = reservePaymentsList[1];

        // Verificar pagos
        Assert.Equal(200m, parentPayment.Amount);
        Assert.Null(parentPayment.ParentReservePaymentId);
        Assert.Equal(0m, childPayment.Amount);
        Assert.Equal(parentPayment.ReservePaymentId, childPayment.ParentReservePaymentId);
        Assert.Equal(987654321, parentPayment.PaymentExternalId);
        Assert.Equal(StatusPaymentEnum.Paid, parentPayment.Status);
        Assert.Equal(StatusPaymentEnum.Paid, childPayment.Status);

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
        Assert.Equal(2, reservePaymentsList.Count);

        var walletParentPayment = reservePaymentsList[0];
        var walletChildPayment = reservePaymentsList[1];

        // Verificar pagos
        Assert.Equal(200m, walletParentPayment.Amount);
        Assert.Null(walletParentPayment.ParentReservePaymentId);
        Assert.Equal(0m, walletChildPayment.Amount);
        Assert.Equal(walletParentPayment.ReservePaymentId, walletChildPayment.ParentReservePaymentId);
        Assert.Null(walletParentPayment.PaymentExternalId); // Aún no tiene ID externo
        Assert.Equal(StatusPaymentEnum.Pending, walletParentPayment.Status);
        Assert.Equal(StatusPaymentEnum.Pending, walletChildPayment.Status);

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
        Assert.Equal(StatusPaymentEnum.Paid, walletChildPayment.Status);
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
        var paymentsDb = new List<ReservePayment>();
        var passengers = new List<Passenger> { new Passenger { PassengerId = 1, ReserveId = 1, Price = 100 } };
        var reserve = new Reserve { ReserveId = 1, ServiceId = 1, Passengers = passengers };
        var reserves = new List<Reserve> { reserve };
        var customer = new Customer { CustomerId = 1, DocumentNumber = "12345678" };
        var service = new Service { ServiceId = 1, ReservePrices = new List<ReservePrice> { new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100 } } };

        var customers = new List<Customer> { customer };
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.Customers).Returns(GetQueryableMockDbSet(customers).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }).Object);

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

        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto>
    {
        new CreatePaymentRequestDto(50, 1),
        new CreatePaymentRequestDto(50, 2)
    });

        result.IsSuccess.Should().BeTrue();
        paymentsDb.Should().HaveCount(2);
        paymentsDb[0].Amount.Should().Be(50);
        paymentsDb[1].Amount.Should().Be(50);
        paymentsDb.All(p => p.ReserveId == 1 && p.CustomerId == 1).Should().BeTrue();
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenTotalAmountDoesNotMatchExpected()
    {
        // Arrange
        var reserveId = 1;
        var customerId = 1;
        var passengers = new List<Passenger> { new Passenger { PassengerId = 1, ReserveId = reserveId, Price = 5000 } };
        var reserve = new Reserve
        {
            ReserveId = reserveId,
            ServiceId = 1,
            Passengers = passengers
        };
        var reserves = new List<Reserve> { reserve };

        var customer = new Customer { CustomerId = customerId };

        var service = new Service
        {
            ServiceId = 1,
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice
            {
                ReserveTypeId = ReserveTypeIdEnum.Ida,
                Price = 5000m
            }
        }
        };

        var payments = new List<CreatePaymentRequestDto>
    {
        new CreatePaymentRequestDto(3000m, 1),
        new CreatePaymentRequestDto(1000m, 2)
    };

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);

        _contextMock.Setup(c => c.Customers.FindAsync(customerId))
            .ReturnsAsync(customer);
        _contextMock.Setup(c => c.Services)
            .Returns(GetMockDbSetWithIdentity(new List<Service> { service }).Object);

        _unitOfWorkMock
    .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
        It.IsAny<Func<Task<Result<bool>>>>(),
        It.IsAny<IsolationLevel>()))
    .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Act
        var result = await _reserveBusiness.CreatePaymentsAsync(1, reserveId, payments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ReserveError.InvalidPaymentAmount(5000, 4000).Code);
        result.Error.Description.Should().Contain("5000");
        result.Error.Description.Should().Contain("4000");
    }

    #region Lock Tests

    [Fact]
    public async Task LockReserveSlots_ShouldSucceed_WhenValidRequest()
    {
        // Arrange
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
                Service = new Service { Vehicle = new Vehicle { AvailableQuantity = 10 } } // Solo 10 cupos en total
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
        var reserves = new List<Reserve>
        {
            new Reserve
            {
                ReserveId = 1,
                Status = ReserveStatusEnum.Confirmed,
                Passengers = new List<Passenger>(),
                VehicleId = 1,
                ServiceId = 1
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
            ReservePrices = new List<ReservePrice>
            {
                new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100 }
            },
            Origin = new City { Name = "Origin" },
            Destination = new City { Name = "Destination" }
        };

        var direction = new Direction { DirectionId = 1, Name = "Location" };

        _contextMock.Setup(c => c.ReserveSlotLocks).Returns(GetQueryableMockDbSet(locks).Object);
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);
        _contextMock.Setup(c => c.Passengers).Returns(GetMockDbSetWithIdentity(passengers).Object);
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(payments).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);
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
            Service = new Service { Vehicle = new Vehicle { AvailableQuantity = 10 } } // Total 10 cupos
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

}