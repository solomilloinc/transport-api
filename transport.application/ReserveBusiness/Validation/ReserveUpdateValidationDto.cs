using FluentValidation;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

internal class ReserveUpdateRequestDtoValidator : AbstractValidator<ReserveUpdateRequestDto>
{
    public ReserveUpdateRequestDtoValidator()
    {
        RuleFor(x => x.VehicleId)
            .GreaterThan(0).When(x => x.VehicleId.HasValue)
            .WithMessage("VehicleId must be greater than 0.");

        RuleFor(x => x.DriverId)
            .GreaterThan(0).When(x => x.DriverId.HasValue)
            .WithMessage("DriverId must be greater than 0.");

        RuleFor(x => x.ReserveDate)
            .GreaterThanOrEqualTo(DateTime.Today).When(x => x.ReserveDate.HasValue)
            .WithMessage("ReserveDate cannot be in the past.");

        RuleFor(x => x.DepartureHour)
            .Must(h => h >= TimeSpan.Zero && h < TimeSpan.FromDays(1)).When(x => x.DepartureHour.HasValue)
            .WithMessage("DepartureHour must be a valid time between 00:00 and 23:59.");

        RuleFor(x => x.Status)
            .InclusiveBetween(0, 99).When(x => x.Status.HasValue)
            .WithMessage("Status must be between 0 and 99.");
    }
}
