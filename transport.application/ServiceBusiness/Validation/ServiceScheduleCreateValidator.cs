using FluentValidation;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness.Validation;

public class ServiceScheduleCreateValidator: AbstractValidator<ServiceScheduleCreateDto>
{
    public ServiceScheduleCreateValidator()
    {
        RuleFor(x => x.ServiceId)
           .GreaterThan(0).WithMessage("El ID del servicio debe ser mayor a 0.");

        RuleFor(x => x.DayOfWeek)
            .InclusiveBetween(0, 6).WithMessage("El día de la semana debe estar entre 0 (domingo) y 6 (sábado).");

        RuleFor(x => x.DepartureHour)
            .NotEqual(TimeSpan.Zero).WithMessage("La hora de salida debe ser mayor a 00:00.");

        RuleFor(x => x.StartDay)
            .IsInEnum().WithMessage("El día de inicio debe ser válido.");

        RuleFor(x => x.EndDay)
            .IsInEnum().WithMessage("El día de fin debe ser válido.");

        RuleFor(x => x)
            .Must(x => x.StartDay <= x.EndDay)
            .WithMessage("El día de inicio no puede ser posterior al día de fin.");
    }
}
