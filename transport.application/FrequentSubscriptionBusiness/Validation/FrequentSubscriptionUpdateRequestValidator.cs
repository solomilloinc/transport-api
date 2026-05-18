using FluentValidation;
using Transport.SharedKernel.Contracts.FrequentSubscription;

namespace Transport.Business.FrequentSubscriptionBusiness.Validation;

public class FrequentSubscriptionUpdateRequestValidator : AbstractValidator<FrequentSubscriptionUpdateRequestDto>
{
    public FrequentSubscriptionUpdateRequestValidator()
    {
        RuleFor(x => x.OutboundPickupLocationId).GreaterThan(0);
        RuleFor(x => x.OutboundDropoffLocationId).GreaterThan(0);
    }
}
