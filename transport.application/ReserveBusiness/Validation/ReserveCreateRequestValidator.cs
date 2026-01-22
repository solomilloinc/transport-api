using FluentValidation;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

public class ReserveCreateRequestValidator : AbstractValidator<ReserveCreateDto>
{
    public ReserveCreateRequestValidator()
    {
        RuleFor(x => x.ReserveDate)
            .NotEmpty()
            .WithMessage("ReserveDate is required.");

        RuleFor(x => x.VehicleId)
            .GreaterThan(0)
            .WithMessage("VehicleId must be greater than 0.");

        RuleFor(x => x.TripId)
            .GreaterThan(0)
            .WithMessage("TripId must be greater than 0.");

        RuleFor(x => x.DepartureHour)
            .NotEmpty()
            .WithMessage("DepartureHour is required.");

        RuleFor(x => x.EstimatedDuration)
            .NotEmpty()
            .WithMessage("EstimatedDuration is required.");
    }
}
