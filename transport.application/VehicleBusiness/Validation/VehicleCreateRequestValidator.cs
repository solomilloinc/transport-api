using FluentValidation;
using Transport.SharedKernel.Contracts.Vehicle;

namespace Transport.Business.VehicleBusiness.Validation;

public class VehicleCreateRequestValidator : AbstractValidator<VehicleCreateRequestDto>
{
    public VehicleCreateRequestValidator()
    {
        RuleFor(p => p.VehicleTypeId)
            .GreaterThan(0)
            .WithMessage("Vehicle type is required");

        RuleFor(p => p.InternalNumber)
            .NotEmpty()
            .WithMessage("Internal number is required")
            .MaximumLength(20)
            .WithMessage("Internal number must not exceed 20 characters");
    }
}
