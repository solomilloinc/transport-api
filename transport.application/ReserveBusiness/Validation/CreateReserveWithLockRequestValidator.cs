using FluentValidation;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

internal class CreateReserveWithLockRequestValidator : AbstractValidator<CreateReserveWithLockRequestDto>
{
    public CreateReserveWithLockRequestValidator()
    {
        RuleFor(x => x.LockToken)
            .NotEmpty()
            .WithMessage("El token de bloqueo es requerido.")
            .Length(36, 50)
            .WithMessage("El token de bloqueo debe tener un formato válido.");

        RuleFor(x => x.LockToken)
            .Must(BeValidGuidFormat)
            .WithMessage("El token de bloqueo debe tener un formato GUID válido.")
            .When(x => !string.IsNullOrEmpty(x.LockToken));

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
            .WithMessage("Debe incluir al menos un pasajero en la reserva.");

        RuleForEach(x => x.Passengers)
            .Must(p => p.Return is not null)
            .WithMessage("Cada pasajero debe tener Return cuando ReserveTypeId es IdaVuelta.")
            .When(x => x.ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta);

        RuleForEach(x => x.Passengers)
            .Must(p => p.Return is null)
            .WithMessage("Pasajeros con reserva Ida no deben llevar Return.")
            .When(x => x.ReserveTypeId == (int)ReserveTypeIdEnum.Ida);

        RuleForEach(x => x.Passengers)
            .SetValidator(new PassengerBookingExternalDtoValidator());

        RuleFor(x => x.Payment)
            .SetValidator(new CreatePaymentExternalRequestValidator())
            .When(x => x.Payment != null);
    }

    private static bool BeValidGuidFormat(string lockToken)
    {
        return Guid.TryParse(lockToken, out _);
    }
}
