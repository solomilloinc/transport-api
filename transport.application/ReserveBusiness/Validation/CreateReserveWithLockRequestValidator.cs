using FluentValidation;
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

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Debe incluir al menos un pasajero en la reserva.");

        RuleForEach(x => x.Items)
            .SetValidator(new PassengerReserveExternalCreateRequestDtoValidator());

        RuleFor(x => x.Payment)
            .SetValidator(new CreatePaymentExternalRequestValidator())
            .When(x => x.Payment != null);

        // Validación adicional: el token debe ser un GUID válido
        RuleFor(x => x.LockToken)
            .Must(BeValidGuidFormat)
            .WithMessage("El token de bloqueo debe tener un formato GUID válido.")
            .When(x => !string.IsNullOrEmpty(x.LockToken));
    }

    private static bool BeValidGuidFormat(string lockToken)
    {
        return Guid.TryParse(lockToken, out _);
    }
}