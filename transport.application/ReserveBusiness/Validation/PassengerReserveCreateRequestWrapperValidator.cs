using FluentValidation;
using Transport.SharedKernel.Contracts.Passenger;

namespace Transport.Business.ReserveBusiness.Validation
{
    public class PassengerReserveCreateRequestWrapperValidator : AbstractValidator<PassengerReserveCreateRequestWrapperDto>
    {
        public PassengerReserveCreateRequestWrapperValidator()
        {
            RuleForEach(x => x.Items).SetValidator(new CustomerReserveCreateRequestDtoValidator());
            RuleForEach(x => x.Payments).SetValidator(new PaymentCreateRequestValidator());
        }
    }
}
