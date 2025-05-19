using FluentValidation;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness.Validation;

public class ServicePriceUpdateRequestValidator : AbstractValidator<ServicePriceUpdateDto>
{
    public ServicePriceUpdateRequestValidator()
    {
        RuleFor(x => x.ReservePriceId)
       .GreaterThan(0)
       .WithMessage("El ID del precio de reserva debe ser mayor a 0");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("El precio debe ser mayor o igual a 0");
    }
}

