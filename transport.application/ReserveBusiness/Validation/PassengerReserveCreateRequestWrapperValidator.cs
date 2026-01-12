using FluentValidation;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Passenger;

namespace Transport.Business.ReserveBusiness.Validation
{
    public class PassengerReserveCreateRequestWrapperValidator : AbstractValidator<PassengerReserveCreateRequestWrapperDto>
    {
        public PassengerReserveCreateRequestWrapperValidator()
        {
            RuleForEach(x => x.Items).SetValidator(new PassengerReserveCreateRequestDtoValidator());
            RuleForEach(x => x.Payments).SetValidator(new PaymentCreateRequestValidator());

            // IdaVuelta requiere pagos obligatoriamente
            RuleFor(x => x.Payments)
                .NotEmpty()
                .WithMessage("Los viajes de ida y vuelta requieren un pago asociado")
                .When(x => x.Items.Any(i => i.ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta));
        }
    }
}
