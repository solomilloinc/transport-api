using FluentValidation;
using Transport.Business.CustomerBusiness.Validation;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

public class CustomerReserveCreateRequestDtoValidator : AbstractValidator<PassengerReserveCreateRequestDto>
{
    public CustomerReserveCreateRequestDtoValidator()
    {
        RuleFor(x => x.ReserveId).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThan(0);

    }
}
