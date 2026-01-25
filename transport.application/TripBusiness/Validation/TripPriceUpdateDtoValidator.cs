using FluentValidation;
using Transport.SharedKernel.Contracts.Trip;

namespace Transport.Business.TripBusiness.Validation;

internal class TripPriceUpdateDtoValidator : AbstractValidator<TripPriceUpdateDto>
{
    public TripPriceUpdateDtoValidator()
    {
        RuleFor(p => p.CityId)
            .GreaterThan(0)
            .WithMessage("CityId must be greater than 0");

        RuleFor(p => p.ReserveTypeId)
            .InclusiveBetween(1, 2)
            .WithMessage("ReserveTypeId must be 1 (Ida) or 2 (IdaVuelta)");

        RuleFor(p => p.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than 0");

        RuleFor(p => p.Order)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Order must be 0 or greater");
    }
}
