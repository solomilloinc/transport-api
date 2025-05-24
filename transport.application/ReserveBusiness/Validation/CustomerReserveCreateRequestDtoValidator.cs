using FluentValidation;
using Transport.Business.CustomerBusiness.Validation;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

public class CustomerReserveCreateRequestDtoValidator : AbstractValidator<CustomerReserveCreateRequestDto>
{
    public CustomerReserveCreateRequestDtoValidator()
    {
        RuleFor(x => x.reserveId).GreaterThan(0);
        RuleFor(x => x.ReserveTypeId).GreaterThan(0);
        RuleFor(x => x.StatusPaymentId).GreaterThan(0);
        RuleFor(x => x.price).GreaterThan(0);

        When(x => x.CustomerId == null, () =>
        {
            RuleFor(x => x.CustomerCreate).NotNull().SetValidator(new CustomerCreateRequestValidator());
        });

        When(x => x.CustomerId != null, () =>
        {
            RuleFor(x => x.CustomerCreate).Null();
        });
    }
}
