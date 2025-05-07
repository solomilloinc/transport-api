using FluentValidation;
using Transport.Business.VehicleBusiness.Validation;
using Transport.SharedKernel.Contracts.VehicleType;

namespace Transport.Business.VehicleTypeBusiness.Validation;

internal class VehicleTypeCreateRequestValidator : AbstractValidator<VehicleTypeCreateRequestDto>
{
    public VehicleTypeCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(50)
            .WithMessage("Name must not exceed 50 characters.");

        RuleFor(x => x.Quantity).NotEmpty()
            .WithMessage("Quantity is required.")
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0.");

        RuleForEach(x => x.Vehicles)
            .SetValidator(new VehicleCreateRequestValidator())
            .When(x => x.Vehicles != null && x.Vehicles.Any());
    }
}
