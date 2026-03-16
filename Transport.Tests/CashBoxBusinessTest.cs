using Xunit;
using Moq;
using FluentAssertions;
using Transport.Business.CashBoxBusiness;
using Transport.Business.Data;
using Transport.Business.Authentication;
using Transport.Domain.CashBoxes;
using Transport.Domain.Users;
using Transport.Domain.Reserves;
using Transport.Domain.Customers;
using Transport.SharedKernel.Contracts.CashBox;

namespace Transport.Tests.CashBoxBusinessTests;

public class CashBoxBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUserContext> _userContextMock;
    private readonly CashBoxBusiness _cashBoxBusiness;

    public CashBoxBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _userContextMock = new Mock<IUserContext>();
        _userContextMock.Setup(u => u.UserId).Returns(1);

        var users = new List<User>
        {
            new User { UserId = 1, Email = "admin@test.com" }
        };
        _contextMock.Setup(c => c.Users).Returns(GetQueryableMockDbSet(users));

        _cashBoxBusiness = new CashBoxBusiness(_contextMock.Object, _userContextMock.Object);
    }

    [Fact]
    public async Task CloseCashBox_ShouldSucceed_AndOpenNewCashBox_WhenNoPendingPayments()
    {
        // Arrange
        var user = new User { UserId = 1, Email = "admin@test.com" };
        var cashBoxes = new List<CashBox>
        {
            new CashBox { CashBoxId = 1, Status = CashBoxStatusEnum.Open, OpenedByUserId = 1, OpenedByUser = user }
        };
        var payments = new List<ReservePayment>();

        _contextMock.Setup(c => c.CashBoxes).Returns(GetMockDbSetWithIdentity(cashBoxes, onAdd: c =>
        {
            c.OpenedByUser = user;
        }));
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(payments));
        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Act
        var result = await _cashBoxBusiness.CloseCashBox(new CloseCashBoxRequestDto("Test Description"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Open"); // Nueva caja abierta
        result.Value.CashBoxId.Should().Be(2); // Nueva caja con ID 2
        cashBoxes.Should().HaveCount(2);
        cashBoxes.First().Status.Should().Be(CashBoxStatusEnum.Closed); // La anterior está cerrada
    }

    // TODO: Habilitar cuando se confirme la funcionalidad de pagos pendientes
    [Fact]
    public async Task CloseCashBox_ShouldFail_WhenHasPendingPayments()
    {
        // Arrange
        var user = new User { UserId = 1, Email = "admin@test.com" };
        var cashBoxes = new List<CashBox>
         {
             new CashBox { CashBoxId = 1, Status = CashBoxStatusEnum.Open, OpenedByUserId = 1, OpenedByUser = user }
         };
        var payments = new List<ReservePayment>
         {
             new ReservePayment { CashBoxId = 1, Status = StatusPaymentEnum.Pending }
         };

        _contextMock.Setup(c => c.CashBoxes).Returns(GetQueryableMockDbSet(cashBoxes));
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(payments));

        // Act
        var result = await _cashBoxBusiness.CloseCashBox(new CloseCashBoxRequestDto("Test Description"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CashBox.CannotCloseWithPendingPayments");
    }

    [Fact]
    public async Task CloseCashBox_ShouldFail_WhenNoOpenCashBox()
    {
        // Arrange
        var cashBoxes = new List<CashBox>();
        _contextMock.Setup(c => c.CashBoxes).Returns(GetQueryableMockDbSet(cashBoxes));

        // Act
        var result = await _cashBoxBusiness.CloseCashBox(new CloseCashBoxRequestDto("Test Description"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CashBox.NoOpenCashBox");
    }

    [Fact]
    public async Task GetCurrentCashBox_ShouldSucceed_WhenOpenCashBoxExists()
    {
        // Arrange
        var user = new User { UserId = 1, Email = "admin@test.com" };
        var cashBoxes = new List<CashBox>
        {
            new CashBox { CashBoxId = 1, Status = CashBoxStatusEnum.Open, OpenedByUserId = 1, OpenedByUser = user }
        };
        var payments = new List<ReservePayment>();

        _contextMock.Setup(c => c.CashBoxes).Returns(GetQueryableMockDbSet(cashBoxes));
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(payments));

        // Act
        var result = await _cashBoxBusiness.GetCurrentCashBox();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CashBoxId.Should().Be(1);
    }

    [Fact]
    public async Task GetCurrentCashBox_ShouldFail_WhenNoOpenCashBox()
    {
        // Arrange
        var cashBoxes = new List<CashBox>();
        _contextMock.Setup(c => c.CashBoxes).Returns(GetQueryableMockDbSet(cashBoxes));

        // Act
        var result = await _cashBoxBusiness.GetCurrentCashBox();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CashBox.NoOpenCashBox");
    }
    [Fact]
    public async Task GetCurrentCashBox_ShouldIncludePaymentBreakdown_WhenPaymentsExist()
    {
        // Arrange
        var user = new User { UserId = 1, Email = "admin@test.com" };
        var cashBoxes = new List<CashBox>
        {
            new CashBox { CashBoxId = 1, Status = CashBoxStatusEnum.Open, OpenedByUserId = 1, OpenedByUser = user }
        };
        
        // 2 pagos padres, uno de ellos con desglose (2 hijos)
        var payments = new List<ReservePayment>
        {
            // Pago 1: Efectivo 100
            new ReservePayment { ReservePaymentId = 1, CashBoxId = 1, Status = StatusPaymentEnum.Paid, Amount = 100, Method = PaymentMethodEnum.Cash },
            
            // Pago 2: Online 200 (Padre que será ignorado para el detalle si hay hijos, pero sumado para el total)
            new ReservePayment { ReservePaymentId = 2, CashBoxId = 1, Status = StatusPaymentEnum.Paid, Amount = 200, Method = PaymentMethodEnum.Online },
            
            // Pago 2 Hijos (Desglose): 150 Online + 50 Cash
            new ReservePayment { ReservePaymentId = 3, CashBoxId = 1, Status = StatusPaymentEnum.Paid, Amount = 150, Method = PaymentMethodEnum.Online, ParentReservePaymentId = 2 },
            new ReservePayment { ReservePaymentId = 4, CashBoxId = 1, Status = StatusPaymentEnum.Paid, Amount = 50, Method = PaymentMethodEnum.Cash, ParentReservePaymentId = 2 }
        };

        _contextMock.Setup(c => c.CashBoxes).Returns(GetQueryableMockDbSet(cashBoxes));
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(payments));

        // Act
        var result = await _cashBoxBusiness.GetCurrentCashBox();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(300); // 100 + 200 (padres)
        result.Value.TotalPayments.Should().Be(2); // Solo padres
        
        // Desglose: 
        // De Pago 1: Cash 100
        // De Pago 2 (Hijos): Online 150 + Cash 50
        // Total: Cash 150, Online 150
        result.Value.PaymentsByMethod.Should().HaveCount(2);
        
        var cashBreakdown = result.Value.PaymentsByMethod.FirstOrDefault(p => p.PaymentMethodName == "Efectivo");
        cashBreakdown.Should().NotBeNull();
        cashBreakdown!.Amount.Should().Be(150);
        
        var onlineBreakdown = result.Value.PaymentsByMethod.FirstOrDefault(p => p.PaymentMethodName == "Online");
        onlineBreakdown.Should().NotBeNull();
        onlineBreakdown!.Amount.Should().Be(150);
    }
}
