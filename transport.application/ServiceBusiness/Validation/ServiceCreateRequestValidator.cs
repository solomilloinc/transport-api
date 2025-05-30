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
        RuleFor(x => x.OriginId)
            .GreaterThan(0)
            .WithMessage("Origin city ID must be greater than 0.");
        RuleFor(x => x.DestinationId)
            .GreaterThan(0)
            .WithMessage("Destination city ID must be greater than 0.");
        RuleFor(x => x.EstimatedDuration)
            .NotEmpty()
            .WithMessage("Estimated duration is required.");
        RuleFor(x => x.VehicleId)
            .GreaterThan(0)
            .WithMessage("Vehicle ID must be greater than 0.");
    }
}
