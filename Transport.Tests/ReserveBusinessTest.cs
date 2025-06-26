using FluentAssertions;
using Moq;
using Transport.Business.Data;
using Transport.Business.ReserveBusiness;
using Transport.Domain.Reserves;
using Transport.Domain.Vehicles;
using Transport.Domain.Drivers;
using Transport.SharedKernel.Contracts.Reserve;
using Xunit;
using static Google.Protobuf.Reflection.SourceCodeInfo.Types;
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
            CustomerReserves = new List<CustomerReserve>(),
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
            Email = "jane@example.com"
        };


        var origin = new Direction { DirectionId = 10, Name = "PickupLocation" };
        var destination = new Direction { DirectionId = 20, Name = "DropoffLocation" };


        // Mock DbSets con identidad para ReservePayment
        var reservePaymentsList = new List<ReservePayment>();
        var reservePaymentsDbSet = GetMockDbSetWithIdentity(reservePaymentsList);
        _contextMock.Setup(c => c.ReservePayments).Returns(reservePaymentsDbSet.Object);

        _contextMock.Setup(c => c.Reserves).Returns(GetMockDbSetWithIdentity(reservesList).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Customers.FindAsync(It.IsAny<int>())).ReturnsAsync(customer);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(destination);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(origin);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        _customerBusinessMock
    .Setup(x => x.GetOrCreateFromPassengerAsync(It.IsAny<CustomerReserveCreateRequestDto>()))
    .ReturnsAsync(Result.Success(customer));

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Construir pasajeros para cada reserva
        var passengers = Enumerable.Range(1, reserveCount).Select(i =>
            new CustomerReserveCreateRequestDto(
                ReserveId: i,
                ReserveTypeId: (int)ReserveTypeIdEnum.IdaVuelta,
                CustomerId: 1,
                IsPayment: true,
                PickupLocationId: 1,
                DropoffLocationId: 2,
                HasTraveled: false,
                Price: 100,
                CustomerCreate: null
            )
        ).ToList();

        // Crear lista de pagos con paymentCount medios, sumando el total esperado
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

        var request = new CustomerReserveCreateRequestWrapperDto(payments, passengers);

        // Act
        var result = await _reserveBusiness.CreatePassengerReserves(request);

        // Assert
        Assert.True(result.IsSuccess);

        // Debe haber tantos pagos como paymentCount
        Assert.Equal(paymentCount, reservePaymentsList.Count);

        var parent = reservePaymentsList.First();

        // El parent debe tener el monto del primer pago y ParentReservePaymentId null
        Assert.Equal(payments[0].TransactionAmount, parent.Amount);
        Assert.Null(parent.ParentReservePaymentId);

        // Los hijos tienen monto 0 y apuntan al padre
        foreach (var child in reservePaymentsList.Skip(1))
        {
            Assert.Equal(0, child.Amount);
            Assert.Equal(parent.ReservePaymentId, child.ParentReservePaymentId);
        }
    }

    [Fact]
    public async Task CreatePassengerReservesExternal_IdaYVuelta_CreatesParentAndChildPayments()
    {
        // Arrange
        var reserve1 = new Reserve
        {
            ReserveId = 1,
            Status = ReserveStatusEnum.Confirmed,
            CustomerReserves = new List<CustomerReserve>(),
            VehicleId = 1,
            ServiceId = 1,
            Driver = new Driver { FirstName = "Mario", LastName = "Bros" }
        };
        var reserve2 = new Reserve
        {
            ReserveId = 2,
            Status = ReserveStatusEnum.Confirmed,
            CustomerReserves = new List<CustomerReserve>(),
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
        _contextMock.Setup(c => c.ReservePayments).Returns(GetMockDbSetWithIdentity(reservePaymentsList).Object);
        _contextMock.Setup(c => c.Reserves).Returns(GetMockDbSetWithIdentity(new List<Reserve> { reserve1, reserve2 }).Object);
        _contextMock.Setup(c => c.Vehicles.FindAsync(It.IsAny<int>())).ReturnsAsync(vehicle);
        _contextMock.Setup(c => c.Services).Returns(GetMockDbSetWithIdentity(new List<Service> { service }).Object);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(destination);
        _contextMock.Setup(c => c.Directions.FindAsync(It.IsAny<int>())).ReturnsAsync(origin);

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
                StatusDetail = "accredited"
            });

        _customerBusinessMock
      .Setup(x => x.GetOrCreateFromPassengerAsync(It.IsAny<CustomerReserveCreateRequestDto>()))
      .ReturnsAsync((CustomerReserveCreateRequestDto dto) =>
      {
          if (dto.CustomerCreate.DocumentNumber == "32145678")
          {
              return Result.Success(new Customer
              {
                  CustomerId = 123,
                  FirstName = "Pepe",
                  LastName = "Argento",
                  DocumentNumber = "32145678",
                  Email = "pepe@example.com"
              });
          }

          return Result.Success(new Customer
          {
              CustomerId = 124,
              FirstName = "Lionel",
              LastName = "Messi",
              DocumentNumber = "32145679",
              Email = "liomessi@example.com"
          });
      });

        var reserveBusinessMock = new Mock<ReserveBusiness>(
    _contextMock.Object,
    _unitOfWorkMock.Object,
    _userContextMock.Object,
    paymentGatewayMock.Object,
    _customerBusinessMock.Object)
        {
            CallBase = true
        };


        var passengers = new List<CustomerReserveCreateRequestDto>
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
            CustomerCreate: new CustomerCreateRequestDto("Pepe", "Argento", "pepe@example.com", "32145678", "1111-2222", null)
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
            CustomerCreate: new CustomerCreateRequestDto("Lionel", "Messi", "liomessi@example.com", "32145679", "1111-2222", null)
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
            IdentificationNumber: "32145678",
            ReserveTypeId: 2
        );

        var request = new CustomerReserveCreateRequestWrapperExternalDto(paymentDto, passengers);

        _unitOfWorkMock
    .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
        It.IsAny<Func<Task<Result<bool>>>>(),
        It.IsAny<IsolationLevel>()))
    .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        // Act
        var result = await reserveBusinessMock.Object.CreatePassengerReservesExternal(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, reservePaymentsList.Count);

        var parent = reservePaymentsList[0];
        var child = reservePaymentsList[1];

        Assert.Equal(200m, parent.Amount);
        Assert.Null(parent.ParentReservePaymentId);
        Assert.Equal(0m, child.Amount);
        Assert.Equal(parent.ReservePaymentId, child.ParentReservePaymentId);
        Assert.Equal(987654321, parent.PaymentExternalId);
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenReserveNotFound()
    {
        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync((Reserve)null);

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
    public async Task CreatePaymentsAsync_ShouldFail_WhenCustomerNotFound()
    {
        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync(new Reserve { ReserveId = 1 });
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync((Customer)null);

        _unitOfWorkMock
            .Setup(uow => uow.ExecuteInTransactionAsync<bool>(
                It.IsAny<Func<Task<Result<bool>>>>(),
                It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>((func, _) => func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto>
    {
        new CreatePaymentRequestDto(1000, 1)
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

        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto>());

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
        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync(new Reserve { ReserveId = 1 });
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
        new CreatePaymentRequestDto(3000m, 1),
        new CreatePaymentRequestDto(1000m, 2)
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
        var result = await _reserveBusiness.CreatePaymentsAsync(reserveId, customerId, payments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ReserveError.InvalidPaymentAmount(5000, 4000).Code);
        result.Error.Description.Should().Contain("5000");
        result.Error.Description.Should().Contain("4000");
    }


}
