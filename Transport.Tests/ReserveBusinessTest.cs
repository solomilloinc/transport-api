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
            _customerBusinessMock.Object);
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
            DocumentNumber = "123456",
            Email = "jane@example.com",
            CurrentBalance = 0
        };

        var origin = new Direction { DirectionId = 10, Name = "PickupLocation" };
        var destination = new Direction { DirectionId = 20, Name = "DropoffLocation" };

        var reservePaymentsList = new List<ReservePayment>();
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(reservePaymentsList).Object);

        var accountTransactionsList = new List<CustomerAccountTransaction>();
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetMockDbSetWithIdentity(accountTransactionsList).Object);

        _contextMock.Setup(c => c.Reserves).Returns(GetMockDbSetWithIdentity(reservesList).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(It.IsAny<int>())).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(origin);

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

        var passengers = Enumerable.Range(1, reserveCount).Select(i =>
            new PassengerReserveCreateRequestDto(
                ReserveId: i,
                ReserveTypeId: (int)ReserveTypeIdEnum.IdaVuelta,
                CustomerId: 1,
                IsPayment: true,
                PickupLocationId: 1,
                DropoffLocationId: 2,
                HasTraveled: false,
                Price: 100,
                FirstName: "Test",
                LastName: "Yuse",
                Email: null,
                Phone1: "222777777",
                DocumentNumber: "12345678"
            )
        ).ToList();

        var totalAmount = 100m * reserveCount;
        var payments = new List<CreatePaymentRequestDto>();

        if (paymentCount == 1)
        {
            payments.Add(new CreatePaymentRequestDto(totalAmount, 1, "123"));
        }
        else
        {
            var partialAmount = totalAmount / paymentCount;
            for (int i = 0; i < paymentCount; i++)
            {
                payments.Add(new CreatePaymentRequestDto(partialAmount, 1, "123"));
            }
        }

        var request = new PassengerReserveCreateRequestWrapperDto(payments, passengers);

        // Act
        var result = await _reserveBusiness.CreatePassengerReserves(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(paymentCount, reservePaymentsList.Count);

        var parent = reservePaymentsList.First();
        Assert.Equal(payments[0].TransactionAmount, parent.Amount);
        Assert.Null(parent.ParentReservePaymentId);

        foreach (var child in reservePaymentsList.Skip(1))
        {
            Assert.Equal(0, child.Amount);
            Assert.Equal(parent.ReservePaymentId, child.ParentReservePaymentId);
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

        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Setup usuario logueado
        _userContextMock.SetupGet(x => x.UserId).Returns(999);
        var user = new User { UserId = 999, CustomerId = 123 };
        _contextMock.Setup(c => c.Users.FindAsync(999)).ReturnsAsync(user);

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
            _customerBusinessMock.Object);

        var passengerList = new List<PassengerReserveCreateRequestDto>
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
        .Setup(uow => uow.ExecuteInTransactionAsync<string>(
            It.IsAny<Func<Task<Result<string>>>>(),
            It.IsAny<IsolationLevel>()))
        .Returns<Func<Task<Result<string>>>, IsolationLevel>((func, _) => func());

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>(async (func, _) => await func());

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
        .Callback<Passenger>(cr => {
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
        Assert.Equal(2, reserve1.Passengers.Count + reserve2.Passengers.Count);

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
        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync((Reserve)null);

        _unitOfWorkMock.Setup(uow => uow.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
                       .Returns<Func<Task<Result<bool>>>, IsolationLevel>(async (func, _) => await func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, new List<CreatePaymentRequestDto>
       {
           new CreatePaymentRequestDto(1000, 1, "12345678")
       });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveError.NotFound);
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenCustomerNotFound()
    {
        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync(new Reserve { ReserveId = 1 });
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync((Customer)null);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, new List<CreatePaymentRequestDto>
    {
        new CreatePaymentRequestDto(1000, 1, "12345678")
    });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CustomerError.NotFound);
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenPaymentsListIsEmpty()
    {
        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync(new Reserve { ReserveId = 1 });
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(new Customer { CustomerId = 1 });

        _unitOfWorkMock
          .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
              It.IsAny<Func<Task<Result<bool>>>>(),
              It.IsAny<IsolationLevel>()))
          .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, new List<CreatePaymentRequestDto>());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Payments.Empty");
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenTransactionAmountIsInvalid()
    {
        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync(new Reserve { ReserveId = 1 });
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(new Customer { CustomerId = 1 });

        _unitOfWorkMock
         .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
             It.IsAny<Func<Task<Result<bool>>>>(),
             It.IsAny<IsolationLevel>()))
         .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, new List<CreatePaymentRequestDto>
    {
        new CreatePaymentRequestDto(0, 1, "12345678"),
        new CreatePaymentRequestDto(-100, 2, "12345678")
    });

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Payments.InvalidAmount");
        result.Error.Description.Should().Contain("Pago #1").And.Contain("Pago #2");
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenPaymentMethodsAreDuplicated()
    {
        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync(new Reserve { ReserveId = 1 });
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(new Customer { CustomerId = 1 });

        _unitOfWorkMock
        .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
            It.IsAny<Func<Task<Result<bool>>>>(),
            It.IsAny<IsolationLevel>()))
        .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, new List<CreatePaymentRequestDto>
    {
        new CreatePaymentRequestDto(1000, 1, "12345678"),
        new CreatePaymentRequestDto(2000, 1, "12345678")
    });

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Payments.DuplicatedMethod");
        result.Error.Description.Should().Contain("Duplicados: 1");
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldSucceed_WhenValidPaymentsProvided()
    {
        var paymentsDb = new List<ReservePayment>();
        var reserve = new Reserve { ReserveId = 1, ServiceId = 1 };
        var customer = new Customer { CustomerId = 1 };
        var service = new Service { ServiceId = 1, ReservePrices = new List<ReservePrice> { new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100 } } };

        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync(reserve);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }).Object);

        var reservePaymentsDbSet = GetMockDbSetWithIdentity(paymentsDb);
        _contextMock.Setup(c => c.ReservePayments).Returns(reservePaymentsDbSet.Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
           .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
               It.IsAny<Func<Task<Result<bool>>>>(),
               It.IsAny<IsolationLevel>()))
           .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, new List<CreatePaymentRequestDto>
    {
        new CreatePaymentRequestDto(50, 1, "12345678"),
        new CreatePaymentRequestDto(50, 2, "12345678")
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
        var reserve = new Reserve
        {
            ReserveId = reserveId,
            ServiceId = 1,
        };

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
        new CreatePaymentRequestDto(3000m, 1, "12345678"),
        new CreatePaymentRequestDto(1000m, 2, "12345678")
    };

        _contextMock.Setup(c => c.Reserves.FindAsync(reserveId))
            .ReturnsAsync(reserve);
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
        var result = await _reserveBusiness.CreatePaymentsAsync(reserveId, payments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ReserveError.InvalidPaymentAmount(5000, 4000).Code);
        result.Error.Description.Should().Contain("5000");
        result.Error.Description.Should().Contain("4000");
    }

}