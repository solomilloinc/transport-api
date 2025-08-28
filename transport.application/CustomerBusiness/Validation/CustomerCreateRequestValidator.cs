using FluentValidation;
using System.Text.RegularExpressions;
using Transport.SharedKernel.Contracts.Customer;

namespace Transport.Business.CustomerBusiness.Validation;

public class CustomerCreateRequestValidator : AbstractValidator<CustomerCreateRequestDto>
{
    public CustomerCreateRequestValidator()
    {
        RuleFor(p => p.FirstName)
            .NotEmpty().WithMessage("El Nombre es requerido")
            .MaximumLength(50).WithMessage("El Nombre no puede exceder los 50 caracteres");

        RuleFor(p => p.LastName)
            .NotEmpty().WithMessage("El Apellido es requerido")
            .MaximumLength(50).WithMessage("El Apellido no puede exceder los 50 caracteres");

        RuleFor(p => p.DocumentNumber)
            .NotEmpty().WithMessage("El Número de documento es requerido")
            .MaximumLength(20).WithMessage("El Número de documento no puede exceder los 20 caracteres");

        RuleFor(p => p.Email)
            .NotEmpty().WithMessage("El Email es requerido")
            .MaximumLength(100).WithMessage("El Email no puede exceder los 100 caracteres")
            .Matches(@"^[\w\.-]+@[\w\.-]+\.\w{2,}$")
            .WithMessage("El Email no tiene un formato válido");
    }
}
