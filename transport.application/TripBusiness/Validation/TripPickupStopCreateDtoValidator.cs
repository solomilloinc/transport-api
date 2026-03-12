using FluentValidation;
using Transport.SharedKernel.Contracts.Trip;

namespace Transport.Business.TripBusiness.Validation;

internal class TripPickupStopCreateDtoValidator : AbstractValidator<TripPickupStopCreateDto>
{
    public TripPickupStopCreateDtoValidator()
    {
        RuleFor(x => x.TripId)
            .GreaterThan(0)
            .WithMessage("TripId must be greater than 0");

        RuleFor(x => x.DirectionId)
            .GreaterThan(0)
            .WithMessage("DirectionId must be greater than 0");

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Order must be 0 or greater");

        RuleFor(x => x.PickupTimeOffset)
            .GreaterThanOrEqualTo(TimeSpan.Zero)
            .WithMessage("PickupTimeOffset must be zero or greater");
    }
}
