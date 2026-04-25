using FluentAssertions;
using Moq;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Business.ReserveBusiness;
using Transport.Business.Services.Payment;
using Transport.Domain.CashBoxes;
using Transport.Domain.CashBoxes.Abstraction;
using Transport.Domain.Customers.Abstraction;
using Transport.Domain.Directions;
using Transport.Domain.Reserves;
using Transport.Domain.Trips;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;
using Xunit;

namespace Transport.Tests;

public class ReserveQuoteTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IUserContext> _userContextMock = new();
    private readonly Mock<IMercadoPagoPaymentGateway> _paymentGatewayMock = new();
    private readonly Mock<ICustomerBusiness> _customerBusinessMock = new();
    private readonly Mock<ICashBoxBusiness> _cashBoxBusinessMock = new();

    private ReserveBusiness BuildBusiness(bool roundTripSameDayOnly = true)
    {
        var openCashBox = new CashBox { CashBoxId = 1, Status = CashBoxStatusEnum.Open };
        _cashBoxBusinessMock.Setup(x => x.GetOpenCashBoxEntity())
            .ReturnsAsync(Result.Success(openCashBox));

        return new ReserveBusiness(
            _contextMock.Object,
            _unitOfWorkMock.Object,
            _userContextMock.Object,
            _paymentGatewayMock.Object,
            _customerBusinessMock.Object,
            new FakeReserveOption(),
            _cashBoxBusinessMock.Object,
            BuildTenantReserveConfigProviderMock(roundTripSameDayOnly).Object);
    }

    /// <summary>
    /// Trip 1: Buenos Aires (1) -> Mar del Plata (2)
    /// Trip 2: Mar del Plata (2) -> Buenos Aires (1)
    /// Both trips have Ida = 100 and IdaVuelta = 80 (per leg discount).
    /// </summary>
    private void SetupTwoTripsWithIdaAndIdaVueltaPrices()
    {
        var trip1 = new Trip
        {
            TripId = 1,
            OriginCityId = 1,
            DestinationCityId = 2,
            Status = EntityStatusEnum.Active,
            Prices = new List<TripPrice>
            {
                new TripPrice { TripId = 1, CityId = 2, ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active },
                new TripPrice { TripId = 1, CityId = 2, ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Price = 80, Status = EntityStatusEnum.Active }
            }
        };
        var trip2 = new Trip
        {
            TripId = 2,
            OriginCityId = 2,
            DestinationCityId = 1,
            Status = EntityStatusEnum.Active,
            Prices = new List<TripPrice>
            {
                new TripPrice { TripId = 2, CityId = 1, ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 100, Status = EntityStatusEnum.Active },
                new TripPrice { TripId = 2, CityId = 1, ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Price = 80, Status = EntityStatusEnum.Active }
            }
        };
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(new List<Trip> { trip1, trip2 }));
        _contextMock.Setup(c => c.Directions).Returns(GetQueryableMockDbSet(new List<Direction>()));
    }

    [Fact]
    public async Task QuoteAsync_ShouldFail_WhenNoItems()
    {
        var business = BuildBusiness();
        var result = await business.QuoteAsync(new ReserveQuoteRequestDto(new List<ReserveQuoteRequestItemDto>()));
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ReserveQuote.NoItems");
    }

    [Fact]
    public async Task QuoteAsync_ShouldApplyComboDiscount_WhenIdaVueltaSameDay()
    {
        SetupTwoTripsWithIdaAndIdaVueltaPrices();
        var business = BuildBusiness(roundTripSameDayOnly: true);

        var date = new DateTime(2025, 6, 1);
        var request = new ReserveQuoteRequestDto(new List<ReserveQuoteRequestItemDto>
        {
            new(TripId: 1, ReserveDate: date, ReserveTypeId: (int)ReserveTypeIdEnum.Ida, DropoffLocationId: null, PassengerCount: 1),
            new(TripId: 2, ReserveDate: date, ReserveTypeId: (int)ReserveTypeIdEnum.IdaVuelta, DropoffLocationId: null, PassengerCount: 1)
        });

        var result = await business.QuoteAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].AppliedReserveTypeId.Should().Be((int)ReserveTypeIdEnum.Ida);
        result.Value.Items[0].UnitPrice.Should().Be(100);
        result.Value.Items[1].AppliedReserveTypeId.Should().Be((int)ReserveTypeIdEnum.IdaVuelta);
        result.Value.Items[1].UnitPrice.Should().Be(80);
        result.Value.Total.Should().Be(180);
        result.Value.DiscountsLost.Should().BeEmpty();
    }

    [Fact]
    public async Task QuoteAsync_ShouldDegradeToIda_WhenIdaVueltaDifferentDay()
    {
        SetupTwoTripsWithIdaAndIdaVueltaPrices();
        var business = BuildBusiness(roundTripSameDayOnly: true);

        var request = new ReserveQuoteRequestDto(new List<ReserveQuoteRequestItemDto>
        {
            new(TripId: 1, ReserveDate: new DateTime(2025, 6, 1), ReserveTypeId: (int)ReserveTypeIdEnum.Ida, DropoffLocationId: null, PassengerCount: 1),
            new(TripId: 2, ReserveDate: new DateTime(2025, 6, 5), ReserveTypeId: (int)ReserveTypeIdEnum.IdaVuelta, DropoffLocationId: null, PassengerCount: 1)
        });

        var result = await business.QuoteAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items[1].RequestedReserveTypeId.Should().Be((int)ReserveTypeIdEnum.IdaVuelta);
        result.Value.Items[1].AppliedReserveTypeId.Should().Be((int)ReserveTypeIdEnum.Ida);
        result.Value.Items[1].UnitPrice.Should().Be(100);
        result.Value.Items[1].Reason.Should().Be(QuoteReasonEnum.RoundTripDifferentDay);
        result.Value.Total.Should().Be(200);
        result.Value.DiscountsLost.Should().ContainSingle(d => d.Code == "RoundTripSameDayOnly");
    }

    [Fact]
    public async Task QuoteAsync_ShouldKeepCombo_WhenRuleDisabled()
    {
        SetupTwoTripsWithIdaAndIdaVueltaPrices();
        var business = BuildBusiness(roundTripSameDayOnly: false);

        var request = new ReserveQuoteRequestDto(new List<ReserveQuoteRequestItemDto>
        {
            new(TripId: 1, ReserveDate: new DateTime(2025, 6, 1), ReserveTypeId: (int)ReserveTypeIdEnum.Ida, DropoffLocationId: null, PassengerCount: 1),
            new(TripId: 2, ReserveDate: new DateTime(2025, 6, 5), ReserveTypeId: (int)ReserveTypeIdEnum.IdaVuelta, DropoffLocationId: null, PassengerCount: 1)
        });

        var result = await business.QuoteAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items[1].AppliedReserveTypeId.Should().Be((int)ReserveTypeIdEnum.IdaVuelta);
        result.Value.Items[1].UnitPrice.Should().Be(80);
        result.Value.Total.Should().Be(180);
        result.Value.DiscountsLost.Should().BeEmpty();
    }

    [Fact]
    public async Task QuoteAsync_ShouldApplyPassengerCount_ToSubtotal()
    {
        SetupTwoTripsWithIdaAndIdaVueltaPrices();
        var business = BuildBusiness();

        var request = new ReserveQuoteRequestDto(new List<ReserveQuoteRequestItemDto>
        {
            new(TripId: 1, ReserveDate: new DateTime(2025, 6, 1), ReserveTypeId: (int)ReserveTypeIdEnum.Ida, DropoffLocationId: null, PassengerCount: 3)
        });

        var result = await business.QuoteAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items[0].UnitPrice.Should().Be(100);
        result.Value.Items[0].Subtotal.Should().Be(300);
        result.Value.Total.Should().Be(300);
    }
}
