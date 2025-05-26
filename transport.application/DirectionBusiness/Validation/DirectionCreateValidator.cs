using FluentValidation;
using Transport.SharedKernel.Contracts.Direction;

namespace Transport.Business.DirectionBusiness.Validation;

public class DirectionCreateValidator : AbstractValidator<DirectionCreateDto>
{
    public DirectionCreateValidator()
    {
        RuleFor(x => x.Name)
                    .NotEmpty().WithMessage("El nombre es obligatorio.")
                    .MaximumLength(150).WithMessage("El nombre no puede superar los 150 caracteres.");

        RuleFor(x => x.CityId)
            .GreaterThan(0).WithMessage("El CityId debe ser mayor a 0.");
    }
}
