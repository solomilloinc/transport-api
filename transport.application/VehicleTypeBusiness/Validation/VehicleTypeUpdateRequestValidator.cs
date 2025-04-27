using FluentValidation;
using Transport.SharedKernel.Contracts.VehicleType;

namespace Transport.Business.VehicleTypeBusiness.Validation;

public class VehicleTypeUpdateRequestValidator: AbstractValidator<VehicleTypeUpdateRequestDto>
{
    public VehicleTypeUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(50)
            .WithMessage("Name must not exceed 50 characters.");

        RuleFor(x => x.Quantity).Empty()
           .WithMessage("Quantity is required.")
           .GreaterThan(0)
           .WithMessage("Quantity must be greater than 0.");
    }
}
