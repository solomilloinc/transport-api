using FluentValidation;
using Transport.SharedKernel.Contracts.Driver;

namespace Transport.Business.DriverBusiness.Validation;

public class DriverUpdateRequestValidator : AbstractValidator<DriverUpdateRequestDto>
{
    public DriverUpdateRequestValidator()
    {
        RuleFor(p => p.FirstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MinimumLength(2)
            .WithMessage("First name must be at least 2 characters long")
            .MaximumLength(50)
            .WithMessage("First name must not exceed 50 characters");

        RuleFor(p => p.LastName)
            .NotEmpty()
            .WithMessage("Last name is required")
            .MinimumLength(2)
            .WithMessage("Last name must be at least 2 characters long")
            .MaximumLength(50)
            .WithMessage("Last name must not exceed 50 characters");
    }
}
