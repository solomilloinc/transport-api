using FluentValidation;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness.Validation;

public class PriceMassiveUpdateRequestValidator : AbstractValidator<PriceMassiveUpdateRequestDto>
{
    public PriceMassiveUpdateRequestValidator()
    {
        RuleFor(x => x.PriceUpdates)
            .NotEmpty()
            .WithMessage("Price updates cannot be empty.");

        RuleForEach<PricePercentageUpdateDto>(x => x.PriceUpdates)
            .ChildRules(priceUpdate =>
            {
                priceUpdate.RuleFor(x => x.ReserveTypeId)
                    .NotEmpty()
                    .WithMessage("Reserve type ID cannot be empty.");
                priceUpdate.RuleFor(x => x.Percentage)
                    .NotEmpty()
                    .WithMessage("Percentage cannot be empty.")
                    .GreaterThan(0)
                    .WithMessage("Percentage must be greater than 0.");
            });
    }
}
