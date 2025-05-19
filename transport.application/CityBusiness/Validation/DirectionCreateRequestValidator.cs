using FluentValidation;
using Transport.SharedKernel.Contracts.City;

namespace Transport.Business.CityBusiness.Validation;

internal class DirectionCreateRequestValidator : AbstractValidator<DirectionCreateRequestDto>
{
    public DirectionCreateRequestValidator()
    {
        RuleFor(p => p.Name)
            .NotEmpty()
            .WithMessage("Direction name is required");

        RuleFor(p => p.Lat)
            .NotNull()
            .WithMessage("Latitude is required")
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90");

        RuleFor(p => p.Lng)
            .NotNull()
            .WithMessage("Longitude is required")
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180");
    }
}
