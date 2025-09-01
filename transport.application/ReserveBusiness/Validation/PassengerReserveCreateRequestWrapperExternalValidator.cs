using FluentValidation;
using Transport.SharedKernel.Contracts.Passenger;

namespace Transport.Business.ReserveBusiness.Validation;

internal class PassengerReserveCreateRequestWrapperExternalValidator : AbstractValidator<PassengerReserveCreateRequestWrapperExternalDto>
{
    public PassengerReserveCreateRequestWrapperExternalValidator()
    {
        RuleFor(x => x.Payment).SetValidator(new CreatePaymentExternalRequestValidator());
        RuleForEach(x => x.Items).SetValidator(new PassengerReserveExternalCreateRequestDtoValidator());
    }
}
