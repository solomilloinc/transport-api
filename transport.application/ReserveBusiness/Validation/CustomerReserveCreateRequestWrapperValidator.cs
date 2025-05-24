using FluentValidation;
using Transport.SharedKernel.Contracts.Customer;

namespace Transport.Business.ReserveBusiness.Validation
{
    public class CustomerReserveCreateRequestWrapperValidator : AbstractValidator<CustomerReserveCreateRequestWrapperDto>
    {
        public CustomerReserveCreateRequestWrapperValidator()
        {
            RuleForEach(x => x.Items).SetValidator(new CustomerReserveCreateRequestDtoValidator());
        }
    }
}
