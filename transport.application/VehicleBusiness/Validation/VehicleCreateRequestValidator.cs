using FluentValidation;
using Transport.SharedKernel.Contracts.Vehicle;

namespace Transport.Business.VehicleBusiness.Validation;

public class VehicleCreateRequestValidator : AbstractValidator<VehicleCreateRequestDto>
{
    public VehicleCreateRequestValidator()
    {
        RuleFor(p => p.InternalNumber)
            .NotEmpty()
            .WithMessage("Internal number is required")
            .MaximumLength(20)
            .WithMessage("Internal number must not exceed 20 characters");

        RuleFor(x => x.AvailableQuantity).NotEmpty()
           .WithMessage("Available Quantity is required.")
           .GreaterThan(0)
           .WithMessage("Available Quantity must be greater than 0.");
    }
}
