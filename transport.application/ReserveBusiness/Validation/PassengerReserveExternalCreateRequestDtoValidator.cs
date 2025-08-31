using FluentValidation;
using Transport.Business.CustomerBusiness.Validation;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

public class PassengerReserveExternalCreateRequestDtoValidator : AbstractValidator<PassengerReserveExternalCreateRequestDto>
{
    public PassengerReserveExternalCreateRequestDtoValidator()
    {
        RuleFor(x => x.ReserveId)
            .GreaterThan(0)
            .WithMessage("El ID de reserva debe ser mayor a 0");
            
        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("El precio debe ser mayor a 0");
            
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("El nombre es requerido")
            .MaximumLength(50)
            .WithMessage("El nombre no puede tener más de 50 caracteres");
            
        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("El apellido es requerido")
            .MaximumLength(50)
            .WithMessage("El apellido no puede tener más de 50 caracteres");
            
        RuleFor(x => x.DocumentNumber)
            .NotEmpty()
            .WithMessage("El número de documento es requerido")
            .MaximumLength(20)
            .WithMessage("El número de documento no puede tener más de 20 caracteres");
    }
}
