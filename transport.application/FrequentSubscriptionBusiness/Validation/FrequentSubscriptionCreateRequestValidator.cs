using FluentValidation;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.FrequentSubscription;

namespace Transport.Business.FrequentSubscriptionBusiness.Validation;

public class FrequentSubscriptionCreateRequestValidator : AbstractValidator<FrequentSubscriptionCreateRequestDto>
{
    public FrequentSubscriptionCreateRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.OutboundServiceId).GreaterThan(0);
        RuleFor(x => x.OutboundPickupLocationId).GreaterThan(0);
        RuleFor(x => x.OutboundDropoffLocationId).GreaterThan(0);

        // El DTO declara ReserveTypeId como int porque transport.common no referencia
        // transport.domain. IsInEnum() no sirve sobre un int — siempre falla.
        // Validamos contra ReserveTypeIdEnum con un Must explícito.
        RuleFor(x => x.ReserveTypeId)
            .Must(v => Enum.IsDefined(typeof(ReserveTypeIdEnum), v))
            .WithMessage("ReserveTypeId debe ser 1 (Ida) o 2 (IdaVuelta).");
    }
}
