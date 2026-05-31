using FluentAssertions;
using Moq;
using Transport.Business.ReserveBusiness.Validation;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;
using Xunit;

namespace Transport.Tests.Validation;

/// <summary>
/// Borde de medianoche: LocalNow = 30-may 22:00 (en UTC ya es 31-may). Las validaciones de
/// "fecha no pasada / no anterior a hoy" deben usar el DÍA LOCAL (30-may), no el UTC (31-may).
/// Si se comparara contra DateTime.Today/UtcNow, una fecha de HOY local quedaría rechazada por error.
/// </summary>
public class DateValidatorTimezoneTests
{
    private static readonly DateTime LocalNow = new(2026, 5, 30, 22, 0, 0, DateTimeKind.Unspecified);

    private static Mock<IDateTimeProvider> Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(x => x.LocalNow).Returns(LocalNow);
        clock.Setup(x => x.UtcNow).Returns(LocalNow.AddHours(3)); // 31-may 01:00 UTC
        return clock;
    }

    // ---- GetReserveReportValidator (búsqueda pública: DepartureDate >= hoy) ----

    [Fact]
    public void GetReserveReport_TodayLocal_IsValid_EvenWhenUtcIsTomorrow()
    {
        var validator = new GetReserveReportValidator(Clock().Object);
        var dto = new ReserveReportFilterRequestDto(
            TripId: 1, TripType: "Ida", Passengers: 1,
            DepartureDate: new DateTime(2026, 5, 30), ReturnDate: null);

        validator.Validate(dto).IsValid.Should().BeTrue("30-may es HOY local, debe aceptarse");
    }

    [Fact]
    public void GetReserveReport_YesterdayLocal_IsInvalid()
    {
        var validator = new GetReserveReportValidator(Clock().Object);
        var dto = new ReserveReportFilterRequestDto(
            TripId: 1, TripType: "Ida", Passengers: 1,
            DepartureDate: new DateTime(2026, 5, 29), ReturnDate: null);

        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(dto.DepartureDate));
    }

    // ---- ReserveUpdateRequestDtoValidator (admin: ReserveDate no en el pasado) ----

    [Fact]
    public void ReserveUpdate_TodayLocal_IsValid_EvenWhenUtcIsTomorrow()
    {
        var validator = new ReserveUpdateRequestDtoValidator(Clock().Object);
        var dto = new ReserveUpdateRequestDto(
            VehicleId: null, DriverId: null,
            ReserveDate: new DateTime(2026, 5, 30), DepartureHour: null, Status: null);

        validator.Validate(dto).IsValid.Should().BeTrue("30-may es HOY local, no es pasado");
    }

    [Fact]
    public void ReserveUpdate_YesterdayLocal_IsInvalid()
    {
        var validator = new ReserveUpdateRequestDtoValidator(Clock().Object);
        var dto = new ReserveUpdateRequestDto(
            VehicleId: null, DriverId: null,
            ReserveDate: new DateTime(2026, 5, 29), DepartureHour: null, Status: null);

        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(dto.ReserveDate));
    }
}
