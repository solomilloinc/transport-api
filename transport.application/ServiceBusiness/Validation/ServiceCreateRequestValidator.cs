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
            .MaximumLength(250)
            .WithMessage("Service name must not exceed 250 characters.");

        RuleFor(x => x.TripId)
            .GreaterThan(0)
            .WithMessage("Trip ID must be greater than 0.");

        RuleFor(x => x.VehicleId)
            .GreaterThan(0)
            .WithMessage("Vehicle ID must be greater than 0.");

        RuleFor(x => x.DayOfWeek)
            .IsInEnum()
            .WithMessage("DayOfWeek must be a valid day of the week.");

        RuleFor(x => x.DepartureHour)
            .NotEqual(TimeSpan.Zero)
            .WithMessage("La hora de salida debe ser mayor a 00:00.");

        RuleFor(x => x.EstimatedDuration)
            .NotEqual(TimeSpan.Zero)
            .WithMessage("La duración estimada debe ser mayor a 00:00.");
    }
}
