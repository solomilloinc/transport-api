using FluentValidation;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness.Validation;

public class ServiceScheduleCreateValidator: AbstractValidator<ServiceScheduleCreateDto>
{
    public ServiceScheduleCreateValidator()
    {
        RuleFor(x => x.DepartureHour)
            .NotEqual(TimeSpan.Zero).WithMessage("La hora de salida debe ser mayor a 00:00.");
    }
}
