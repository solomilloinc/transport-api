using FluentValidation;
using Transport.SharedKernel.Contracts.Customer.Reserve;

namespace Transport.Business.CustomerBusiness.Validation;

public class CustomerReserveCreateRequestValidator : AbstractValidator<CustomerReserveCreateRequestDto>
{
    public CustomerReserveCreateRequestValidator()
    {
        RuleFor(p => p.ReserveId).LessThan(0).WithMessage("No ha seleccionado la reserva");
        RuleFor(p => p.CustomerId).LessThan(0).WithMessage("No ha seleccionado el cliente");
        RuleFor(p => p.PickupLocationId).LessThan(0).WithMessage("No ha seleccionado la ubicación de recogida");
        RuleFor(p => p.DropoffLocationId).LessThan(0).WithMessage("No ha seleccionado la ubicación de destino");
    }
}
