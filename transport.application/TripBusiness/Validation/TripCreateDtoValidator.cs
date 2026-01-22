using FluentValidation;
using Transport.SharedKernel.Contracts.Trip;

namespace Transport.Business.TripBusiness.Validation;

internal class TripCreateDtoValidator : AbstractValidator<TripCreateDto>
{
    public TripCreateDtoValidator()
    {
        RuleFor(p => p.Description)
            .NotEmpty()
            .WithMessage("Description is required")
            .MaximumLength(200)
            .WithMessage("Description must not exceed 200 characters");

        RuleFor(p => p.OriginCityId)
            .GreaterThan(0)
            .WithMessage("OriginCityId must be greater than 0");

        RuleFor(p => p.DestinationCityId)
            .GreaterThan(0)
            .WithMessage("DestinationCityId must be greater than 0")
            .NotEqual(p => p.OriginCityId)
            .WithMessage("DestinationCityId must be different from OriginCityId");
    }
}
