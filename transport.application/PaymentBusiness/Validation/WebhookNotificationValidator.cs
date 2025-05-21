using FluentValidation;
using Transport.SharedKernel.Contracts.Payment;

namespace Transport.Business.PaymentBusiness.Validation;

public class WebhookNotificationValidator : AbstractValidator<WebhookNotification>
{
    public WebhookNotificationValidator()
    {
        RuleFor(x => x)
            .NotNull()
            .WithMessage("Notification must not be null");

        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required")
            .Must(id => long.TryParse(id, out _))
            .WithMessage("Id must be a valid number");

        RuleFor(x => x.Topic)
            .NotEmpty()
            .WithMessage("Topic is required")
            .Equal("payment")
            .WithMessage("Only 'payment' topic is supported");
    }
}
