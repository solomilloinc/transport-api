using FluentValidation;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness.Validation;

/// <summary>
/// Valida el payload del endpoint bulk sync de schedules.
/// Chequea forma del payload (lista no null, items con hora válida, sin duplicados
/// de ServiceScheduleId dentro del mismo payload). Validaciones que requieren
/// acceso a la DB (existencia del servicio, pertenencia del scheduleId al servicio)
/// viven en <c>ServiceBusiness.SyncSchedules</c>.
/// </summary>
public class ServiceSchedulesSyncRequestValidator : AbstractValidator<ServiceSchedulesSyncRequestDto>
{
    public ServiceSchedulesSyncRequestValidator()
    {
        RuleFor(x => x.Schedules)
            .NotNull()
            .WithMessage("Schedules list is required (send an empty array to remove all schedules).");

        // Cada item: hora > 00:00 y, si trae Id, que sea > 0.
        RuleForEach(x => x.Schedules).ChildRules(item =>
        {
            item.RuleFor(i => i.DepartureHour)
                .NotEqual(TimeSpan.Zero)
                .WithMessage("DepartureHour must be greater than 00:00:00.");

            // ServiceScheduleId = null significa "crear". Si viene con valor, debe ser > 0
            // (evita que el frontend mande un 0 por accidente y caiga en "no encontrado"
            // cuando en realidad el payload es inválido).
            item.RuleFor(i => i.ServiceScheduleId)
                .Must(id => id is null || id > 0)
                .WithMessage("ServiceScheduleId must be null (for new schedules) or a positive integer.");
        });

        // No aceptar duplicados de ServiceScheduleId dentro del mismo payload —
        // señal segura de bug en el frontend (mismo schedule enviado 2 veces).
        RuleFor(x => x.Schedules)
            .Must(list =>
            {
                var ids = list.Where(i => i.ServiceScheduleId.HasValue)
                              .Select(i => i.ServiceScheduleId!.Value)
                              .ToList();
                return ids.Count == ids.Distinct().Count();
            })
            .When(x => x.Schedules is not null)
            .WithMessage("Duplicate ServiceScheduleId detected in the payload.");
    }
}
