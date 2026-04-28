using FluentAssertions;
using Transport.Business.ReserveBusiness.Internal;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Reserve;
using Xunit;

namespace Transport.Tests;

public class ReservePassengerItemsValidatorTests
{
    private static PassengerReserveExternalCreateRequestDto Item(int reserveId, ReserveTypeIdEnum type) =>
        new(
            ReserveId: reserveId,
            ReserveTypeId: (int)type,
            CustomerId: null,
            IsPayment: false,
            PickupLocationId: 1,
            DropoffLocationId: 2,
            HasTraveled: false,
            Price: 0m,
            FirstName: "A",
            LastName: "B",
            Email: null,
            Phone1: "1",
            DocumentNumber: "1");

    [Fact]
    public void Validate_Succeeds_SingleReserve_Ida()
    {
        var r = ReservePassengerItemsValidator.ValidateUserReserveCombination(
            new List<PassengerReserveExternalCreateRequestDto> { Item(1, ReserveTypeIdEnum.Ida) });
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Succeeds_TwoReserves_IdaAndIdaVuelta_SameCalendarDay()
    {
        var d = new DateTime(2026, 6, 1);
        var dates = new Dictionary<int, DateTime> { [1] = d, [2] = d };
        var r = ReservePassengerItemsValidator.ValidateUserReserveCombination(
            new List<PassengerReserveExternalCreateRequestDto>
            {
                Item(1, ReserveTypeIdEnum.Ida),
                Item(2, ReserveTypeIdEnum.IdaVuelta)
            },
            dates,
            roundTripSameDayOnly: true);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Succeeds_TwoReserves_IdaAndIdaVuelta_DifferentCalendarDays()
    {
        var dates = new Dictionary<int, DateTime> { [1] = new DateTime(2026, 6, 1), [2] = new DateTime(2026, 6, 5) };
        var r = ReservePassengerItemsValidator.ValidateUserReserveCombination(
            new List<PassengerReserveExternalCreateRequestDto>
            {
                Item(1, ReserveTypeIdEnum.Ida),
                Item(2, ReserveTypeIdEnum.IdaVuelta)
            },
            dates,
            roundTripSameDayOnly: true);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Succeeds_TwoReserves_IdaAndIda_WhenDifferentDays_AndRoundTripSameDayOnly()
    {
        var dates = new Dictionary<int, DateTime> { [1] = new DateTime(2026, 6, 1), [2] = new DateTime(2026, 6, 5) };
        var r = ReservePassengerItemsValidator.ValidateUserReserveCombination(
            new List<PassengerReserveExternalCreateRequestDto>
            {
                Item(1, ReserveTypeIdEnum.Ida),
                Item(2, ReserveTypeIdEnum.Ida)
            },
            dates,
            roundTripSameDayOnly: true);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_TwoReserves_IdaAndIda_SameCalendarDay()
    {
        var d = new DateTime(2026, 6, 1);
        var dates = new Dictionary<int, DateTime> { [1] = d, [2] = d };
        var r = ReservePassengerItemsValidator.ValidateUserReserveCombination(
            new List<PassengerReserveExternalCreateRequestDto>
            {
                Item(1, ReserveTypeIdEnum.Ida),
                Item(2, ReserveTypeIdEnum.Ida)
            },
            dates,
            roundTripSameDayOnly: true);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_TwoReserves_IdaAndIda_DifferentDays_WhenNotRoundTripSameDayOnly()
    {
        var dates = new Dictionary<int, DateTime> { [1] = new DateTime(2026, 6, 1), [2] = new DateTime(2026, 6, 5) };
        var r = ReservePassengerItemsValidator.ValidateUserReserveCombination(
            new List<PassengerReserveExternalCreateRequestDto>
            {
                Item(1, ReserveTypeIdEnum.Ida),
                Item(2, ReserveTypeIdEnum.Ida)
            },
            dates,
            roundTripSameDayOnly: false);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_TwoReserves_IdaAndIda_DifferentDays_MissingReserveDate()
    {
        var dates = new Dictionary<int, DateTime> { [1] = new DateTime(2026, 6, 1) };
        var r = ReservePassengerItemsValidator.ValidateUserReserveCombination(
            new List<PassengerReserveExternalCreateRequestDto>
            {
                Item(1, ReserveTypeIdEnum.Ida),
                Item(2, ReserveTypeIdEnum.Ida)
            },
            dates,
            roundTripSameDayOnly: true);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_TwoReserves_BothIdaVuelta()
    {
        var dates = new Dictionary<int, DateTime> { [1] = new DateTime(2026, 6, 1), [2] = new DateTime(2026, 6, 5) };
        var r = ReservePassengerItemsValidator.ValidateUserReserveCombination(
            new List<PassengerReserveExternalCreateRequestDto>
            {
                Item(1, ReserveTypeIdEnum.IdaVuelta),
                Item(2, ReserveTypeIdEnum.IdaVuelta)
            },
            dates,
            roundTripSameDayOnly: true);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_TwoReserves_IdaAndIda_DifferentDays_WhenReserveDatesNotPassed()
    {
        var r = ReservePassengerItemsValidator.ValidateUserReserveCombination(
            new List<PassengerReserveExternalCreateRequestDto>
            {
                Item(1, ReserveTypeIdEnum.Ida),
                Item(2, ReserveTypeIdEnum.Ida)
            });
        r.IsFailure.Should().BeTrue();
    }
}
