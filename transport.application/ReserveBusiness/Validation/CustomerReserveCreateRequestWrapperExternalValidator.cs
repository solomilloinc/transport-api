using FluentValidation;
using Transport.SharedKernel.Contracts.Customer;

namespace Transport.Business.ReserveBusiness.Validation;

internal class CustomerReserveCreateRequestWrapperExternalValidator : AbstractValidator<CustomerReserveCreateRequestWrapperExternalDto>
{
    public CustomerReserveCreateRequestWrapperExternalValidator()
    {
        RuleFor(x => x.Payment).SetValidator(new CreatePaymentExternalRequestValidator());
        RuleForEach(x => x.Items).SetValidator(new CustomerReserveCreateRequestDtoValidator());
    }
}
