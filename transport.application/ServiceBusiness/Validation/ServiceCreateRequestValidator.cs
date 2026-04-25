using FluentValidation;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness.Validation;

public class ServiceCreateRequestValidator : AbstractValidator<ServiceCreateRequestDto>
{
    public ServiceCreateRequestValidator()
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

        // StartDay / EndDay: ambos deben ser valores válidos de DayOfWeek (0-6).
        //
        // Notas técnicas sobre la validación:
        //  - IsInEnum() garantiza que el entero recibido esté dentro de 0..6. Rechaza 99,
        //    -1, etc. con un 400.
        //  - NO se rechaza el valor 0 (Sunday): es un día legítimo. La "obligatoriedad no
        //    default" la garantiza el tipo no-nullable del record positional: si el cliente
        //    omite el campo en el JSON, System.Text.Json fallará al deserializar antes de
        //    llegar al validator (campo requerido del constructor).
        //  - NO se valida StartDay <= EndDay porque Service.IsDayWithinScheduleRange soporta
        //    wraparound intencionalmente: p. ej. StartDay=5 (Viernes), EndDay=1 (Lunes)
        //    representa un servicio de fin de semana que opera Vie-Sáb-Dom-Lun.
        RuleFor(x => x.StartDay)
            .IsInEnum()
            .WithMessage("StartDay must be a valid DayOfWeek (0=Sunday ... 6=Saturday).");

        RuleFor(x => x.EndDay)
            .IsInEnum()
            .WithMessage("EndDay must be a valid DayOfWeek (0=Sunday ... 6=Saturday).");

        RuleForEach(x => x.Schedules)
       .SetValidator(new ServiceScheduleCreateValidator());
    }
}
