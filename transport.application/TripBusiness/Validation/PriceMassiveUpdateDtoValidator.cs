using FluentValidation;
using Transport.SharedKernel.Contracts.Trip;

namespace Transport.Business.TripBusiness.Validation;

internal class PriceMassiveUpdateDtoValidator : AbstractValidator<PriceMassiveUpdateDto>
{
    public PriceMassiveUpdateDtoValidator()
    {
        RuleFor(p => p.PriceUpdates)
            .NotEmpty()
            .WithMessage("PriceUpdates is required");

        RuleForEach(p => p.PriceUpdates).SetValidator(new PriceUpdateItemValidator());
    }
}

internal class PriceUpdateItemValidator : AbstractValidator<PriceUpdateItem>
{
    public PriceUpdateItemValidator()
    {
        RuleFor(p => p.ReserveTypeId)
            .InclusiveBetween(1, 2)
            .WithMessage("ReserveTypeId must be 1 (Ida) or 2 (IdaVuelta)");

        RuleFor(p => p.Percentage)
            .NotEqual(0)
            .WithMessage("Percentage cannot be 0");
    }
}
