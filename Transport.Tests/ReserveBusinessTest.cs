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

namespace Transport.Tests.ReserveBusinessTests;

public class ReserveBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ReserveBusiness _reserveBusiness;

    public ReserveBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _reserveBusiness = new ReserveBusiness(_contextMock.Object, _unitOfWorkMock.Object);
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

        // Setup SaveChanges
        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Setup ExecuteInTransactionAsync para ejecutar el delegado normalmente
        _unitOfWorkMock
    .Setup(uow => uow.ExecuteInTransactionAsync<Result<bool>>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
    .Returns<Func<Task<Result<bool>>>, IsolationLevel>(async (func, _) => await func());

        // Construir pasajeros para cada reserva
        var passengers = Enumerable.Range(1, reserveCount).Select(i =>
            new CustomerReserveCreateRequestDto(
                reserveId: i,
                ReserveTypeId: (int)ReserveTypeIdEnum.IdaVuelta,
                CustomerId: 1,
                IsPayment: true,
                PickupLocationId: 1,
                DropoffLocationId: 2,
                HasTraveled: false,
                price: 100,
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
    public async Task CreatePaymentsAsync_ShouldFail_WhenReserveNotFound()
    {
        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync((Reserve)null);

        _unitOfWorkMock.Setup(uow => uow.ExecuteInTransactionAsync<Result<bool>>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
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

        _unitOfWorkMock.Setup(uow => uow.ExecuteInTransactionAsync<Result<bool>>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
                       .Returns<Func<Task<Result<bool>>>, IsolationLevel>(async (func, _) => await func());


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

        _unitOfWorkMock.Setup(uow => uow.ExecuteInTransactionAsync<Result<bool>>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
               .Returns<Func<Task<Result<bool>>>, IsolationLevel>(async (func, _) => await func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto>());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Payments.Empty");
    }

    [Fact]
    public async Task CreatePaymentsAsync_ShouldFail_WhenTransactionAmountIsInvalid()
    {
        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync(new Reserve { ReserveId = 1 });
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(new Customer { CustomerId = 1 });

        _unitOfWorkMock.Setup(uow => uow.ExecuteInTransactionAsync<Result<bool>>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
               .Returns<Func<Task<Result<bool>>>, IsolationLevel>(async (func, _) => await func());

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

        _unitOfWorkMock.Setup(uow => uow.ExecuteInTransactionAsync<Result<bool>>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
               .Returns<Func<Task<Result<bool>>>, IsolationLevel>(async (func, _) => await func());

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
        var reserve = new Reserve { ReserveId = 1 };
        var customer = new Customer { CustomerId = 1 };

        _contextMock.Setup(c => c.Reserves.FindAsync(1)).ReturnsAsync(reserve);
        _contextMock.Setup(c => c.Customers.FindAsync(1)).ReturnsAsync(customer);

        var reservePaymentsDbSet = GetMockDbSetWithIdentity(paymentsDb);
        _contextMock.Setup(c => c.ReservePayments).Returns(reservePaymentsDbSet.Object);
        SetupSaveChangesWithOutboxAsync(_contextMock);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync<Result<bool>>(It.IsAny<Func<Task<Result<bool>>>>(), It.IsAny<IsolationLevel>()))
            .Returns<Func<Task<Result<bool>>>, IsolationLevel>(async (func, _) => await func());

        var result = await _reserveBusiness.CreatePaymentsAsync(1, 1, new List<CreatePaymentRequestDto>
    {
        new CreatePaymentRequestDto(1500, 1),
        new CreatePaymentRequestDto(3000, 2)
    });

        result.IsSuccess.Should().BeTrue();
        paymentsDb.Should().HaveCount(2);
        paymentsDb[0].Amount.Should().Be(1500);
        paymentsDb[1].Amount.Should().Be(3000);
        paymentsDb.All(p => p.ReserveId == 1 && p.CustomerId == 1).Should().BeTrue();
    }


}
