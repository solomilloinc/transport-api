using FluentValidation;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Passenger;

namespace Transport.Business.ReserveBusiness.Validation;

internal class PassengerReserveCreateRequestWrapperExternalValidator : AbstractValidator<PassengerReserveCreateRequestWrapperExternalDto>
{
    public PassengerReserveCreateRequestWrapperExternalValidator()
    {
        RuleFor(x => x.ReserveTypeId)
            .Must(t => t == (int)ReserveTypeIdEnum.Ida || t == (int)ReserveTypeIdEnum.IdaVuelta)
            .WithMessage("ReserveTypeId debe ser Ida (1) o IdaVuelta (2).");

        RuleFor(x => x.ReturnReserveId)
            .NotNull()
            .WithMessage("ReturnReserveId es obligatorio para IdaVuelta.")
            .When(x => x.ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta);

        RuleFor(x => x.ReturnReserveId)
            .Null()
            .WithMessage("ReturnReserveId no debe enviarse para reservas de tipo Ida.")
            .When(x => x.ReserveTypeId == (int)ReserveTypeIdEnum.Ida);

        RuleFor(x => x)
            .Must(x => x.ReturnReserveId is null || x.ReturnReserveId.Value != x.OutboundReserveId)
            .WithMessage("OutboundReserveId y ReturnReserveId deben ser distintos.");

        RuleFor(x => x.Passengers)
            .NotEmpty()
            .WithMessage("Debe enviar al menos un pasajero.");

        RuleForEach(x => x.Passengers)
            .Must(p => p.Return is not null)
            .WithMessage("Cada pasajero debe tener Return cuando ReserveTypeId es IdaVuelta.")
            .When(x => x.ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta);

        RuleForEach(x => x.Passengers)
            .Must(p => p.Return is null)
            .WithMessage("Pasajeros con reserva Ida no deben llevar Return.")
            .When(x => x.ReserveTypeId == (int)ReserveTypeIdEnum.Ida);

        RuleForEach(x => x.Passengers).SetValidator(new PassengerBookingExternalDtoValidator());
        RuleFor(x => x.Payment).SetValidator(new CreatePaymentExternalRequestValidator()).When(x => x.Payment is not null);
    }
}
