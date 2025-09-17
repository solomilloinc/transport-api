using FluentValidation;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

internal class LockReserveSlotsRequestValidator : AbstractValidator<LockReserveSlotsRequestDto>
{
    public LockReserveSlotsRequestValidator()
    {
        RuleFor(x => x.OutboundReserveId)
            .GreaterThan(0)
            .WithMessage("El ID de reserva de ida debe ser mayor a 0.");

        When(x => x.ReturnReserveId.HasValue, () => {
            RuleFor(x => x.ReturnReserveId.Value)
                .GreaterThan(0)
                .WithMessage("El ID de reserva de vuelta debe ser mayor a 0.");
        });

        RuleFor(x => x.PassengerCount)
            .GreaterThan(0)
            .WithMessage("La cantidad de pasajeros debe ser mayor a 0.")
            .LessThanOrEqualTo(50)
            .WithMessage("La cantidad de pasajeros no puede exceder 50.");

        // Validación de lógica: no se puede tener el mismo ID para ida y vuelta
        RuleFor(x => x)
            .Must(x => !x.ReturnReserveId.HasValue || x.OutboundReserveId != x.ReturnReserveId.Value)
            .WithMessage("El ID de reserva de ida no puede ser igual al de vuelta.");
    }
}