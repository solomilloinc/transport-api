using FluentValidation;
using Transport.SharedKernel.Contracts.Driver;

namespace Transport.Business.DriverBusiness.Validation;

public class DriverCreateRequestValidator : AbstractValidator<DriverCreateRequestDto>
{
    public DriverCreateRequestValidator()
    {
        RuleFor(p => p.firstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MinimumLength(2)
            .WithMessage("First name must be at least 2 characters long")
            .MaximumLength(50)
            .WithMessage("First name must not exceed 50 characters");
        RuleFor(p => p.lastName)
            .NotEmpty()
            .WithMessage("Last name is required")
            .MinimumLength(2)
            .WithMessage("Last name must be at least 2 characters long")
            .MaximumLength(50)
            .WithMessage("Last name must not exceed 50 characters");

        RuleFor(p => p.documentNumber)
            .NotEmpty()
            .WithMessage("Document number is required")
            .MaximumLength(20)
            .WithMessage("Document number must not exceed 20 characters")
            .Matches(@"^\d{8,10}$")
            .WithMessage("Document number must be between 8 and 10 digits");
    }
}
