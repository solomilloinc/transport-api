using FluentValidation;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness.Validation;

public class ServicePriceAddRequestValidator: AbstractValidator<ServicePriceAddDto>
{
    public ServicePriceAddRequestValidator()
    {
        RuleFor(x => x.ReserveTypeId)
          .Must(value => Enum.IsDefined(typeof(ReserveTypeIdEnum), value))
          .WithMessage("El tipo de reserva es inválido");

        RuleFor(p => p.Price)
            .NotEmpty().WithMessage("El precio es requerido")
            .GreaterThan(0).WithMessage("El precio debe ser mayor a 0");
    }
}
