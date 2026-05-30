using FluentValidation;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Passenger;

namespace Transport.Business.ReserveBusiness.Validation
{
    public class PassengerReserveCreateRequestWrapperValidator : AbstractValidator<PassengerReserveCreateRequestWrapperDto>
    {
        public PassengerReserveCreateRequestWrapperValidator()
        {
            RuleFor(x => x.ReserveTypeId)
                .Must(t => t == (int)ReserveTypeIdEnum.Ida || t == (int)ReserveTypeIdEnum.IdaVuelta)
                .WithMessage("ReserveTypeId debe ser Ida (1) o IdaVuelta (2).");

            // Rule 1: IdaVuelta requires ReturnReserveId
            RuleFor(x => x.ReturnReserveId)
                .NotNull()
                .WithMessage("ReturnReserveId es obligatorio para IdaVuelta.")
                .When(x => x.ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta);

            // Rule 2: Ida must not include ReturnReserveId
            RuleFor(x => x.ReturnReserveId)
                .Null()
                .WithMessage("ReturnReserveId no debe enviarse para reservas de tipo Ida.")
                .When(x => x.ReserveTypeId == (int)ReserveTypeIdEnum.Ida);

            // Rule 5: OutboundReserveId != ReturnReserveId
            RuleFor(x => x)
                .Must(x => x.ReturnReserveId is null || x.ReturnReserveId.Value != x.OutboundReserveId)
                .WithMessage("OutboundReserveId y ReturnReserveId deben ser distintos.");

            // Rule 6: At least one passenger
            RuleFor(x => x.Passengers)
                .NotEmpty()
                .WithMessage("Debe enviar al menos un pasajero.");

            // Rule 3: IdaVuelta requires Return on each passenger
            RuleForEach(x => x.Passengers)
                .Must(p => p.Return is not null)
                .WithMessage("Cada pasajero debe tener Return cuando ReserveTypeId es IdaVuelta.")
                .When(x => x.ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta);

            // Rule 4: Ida must not have Return on any passenger
            RuleForEach(x => x.Passengers)
                .Must(p => p.Return is null)
                .WithMessage("Pasajeros con reserva Ida no deben llevar Return.")
                .When(x => x.ReserveTypeId == (int)ReserveTypeIdEnum.Ida);

            RuleForEach(x => x.Passengers).SetValidator(new PassengerBookingDtoValidator());
            RuleForEach(x => x.Payments).SetValidator(new PaymentCreateRequestValidator());

        }
    }
}
