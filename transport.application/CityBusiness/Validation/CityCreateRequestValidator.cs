using FluentValidation;
using Transport.SharedKernel.Contracts.City;

namespace Transport.Business.CityBusiness.Validation;

internal class CityCreateRequestValidator : AbstractValidator<CityCreateRequestDto>
{
    public CityCreateRequestValidator()
    {
        RuleFor(p => p.Code)
            .NotEmpty()
            .WithMessage("Code is required")
            .MinimumLength(2)
            .WithMessage("Code must be at least 2 characters long")
            .MaximumLength(50)
            .WithMessage("Code must not exceed 50 characters");

        RuleFor(p => p.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MinimumLength(2)
            .WithMessage("Name must be at least 2 characters long")
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters");
    }
}
