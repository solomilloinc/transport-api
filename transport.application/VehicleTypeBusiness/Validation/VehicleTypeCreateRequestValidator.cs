using FluentValidation;
using Transport.Business.VehicleBusiness.Validation;
using Transport.SharedKernel.Contracts.VehicleType;

namespace Transport.Business.VehicleTypeBusiness.Validation;

internal class VehicleTypeCreateRequestValidator: AbstractValidator<VehicleTypeCreateRequestDto>
{
    public VehicleTypeCreateRequestValidator()
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

        RuleFor(x => x.Vehicles)
            .NotEmpty()
            .WithMessage("At least one vehicle is required.")
            .ForEach(vehicle => vehicle.SetValidator(new VehicleCreateRequestValidator()))
            .WithMessage("Each vehicle must be valid.");
    }
}
