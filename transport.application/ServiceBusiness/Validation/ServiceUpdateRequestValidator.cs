using FluentValidation;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness.Validation;

/// <summary>
/// Validador para el payload de actualización de Service.
/// Replica las reglas comunes con <see cref="ServiceCreateRequestValidator"/> pero
/// omite Schedules, que no forma parte del DTO de update (ver XML doc de
/// <see cref="ServiceUpdateRequestDto"/>). <c>TripId</c> sí es editable — la
/// validación de existencia/estado del Trip ocurre en <c>ServiceBusiness.Update</c>
/// (no se puede hacer acá porque FluentValidation no tiene acceso al DbContext).
/// </summary>
public class ServiceUpdateRequestValidator : AbstractValidator<ServiceUpdateRequestDto>
{
    public ServiceUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Service name is required.")
            .MaximumLength(100)
            .WithMessage("Service name must not exceed 100 characters.");

        RuleFor(x => x.TripId)
            .GreaterThan(0)
            .WithMessage("Trip ID must be greater than 0.");

        RuleFor(x => x.EstimatedDuration)
            .NotEmpty()
            .WithMessage("Estimated duration is required.");

        RuleFor(x => x.VehicleId)
            .GreaterThan(0)
            .WithMessage("Vehicle ID must be greater than 0.");

        // StartDay / EndDay: obligatorios y dentro del rango 0-6. Ver comentario extenso
        // en ServiceCreateRequestValidator para la justificación completa.
        // Se permite wraparound (StartDay > EndDay) porque el helper del dominio lo soporta.
        RuleFor(x => x.StartDay)
            .IsInEnum()
            .WithMessage("StartDay must be a valid DayOfWeek (0=Sunday ... 6=Saturday).");

        RuleFor(x => x.EndDay)
            .IsInEnum()
            .WithMessage("EndDay must be a valid DayOfWeek (0=Sunday ... 6=Saturday).");
    }
}
