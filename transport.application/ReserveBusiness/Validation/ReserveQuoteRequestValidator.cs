using FluentValidation;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

public class ReserveQuoteRequestValidator : AbstractValidator<ReserveQuoteRequestDto>
{
    public ReserveQuoteRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item is required.")
            .Must(items => items.Count <= 2).WithMessage("A quote can include at most 2 items (Ida + IdaVuelta).");

        RuleForEach(x => x.Items).SetValidator(new ReserveQuoteRequestItemValidator());

        // A lone IdaVuelta item without a paired Ida is not a valid combination.
        RuleFor(x => x.Items)
            .Must(items => !(items.Count == 1 && items[0].ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta))
            .WithMessage("Cannot quote only the return leg without the outbound leg.");

        // If two items are provided, the only valid combination is Ida + IdaVuelta.
        RuleFor(x => x.Items)
            .Must(items =>
            {
                if (items.Count != 2) return true;
                var types = items.Select(i => i.ReserveTypeId).OrderBy(t => t).ToArray();
                return types[0] == (int)ReserveTypeIdEnum.Ida && types[1] == (int)ReserveTypeIdEnum.IdaVuelta;
            })
            .WithMessage("The only valid two-item combination is exactly Ida + IdaVuelta.");
    }
}

public class ReserveQuoteRequestItemValidator : AbstractValidator<ReserveQuoteRequestItemDto>
{
    public ReserveQuoteRequestItemValidator()
    {
        RuleFor(x => x.TripId).GreaterThan(0);
        RuleFor(x => x.ReserveTypeId)
            .Must(t => t == (int)ReserveTypeIdEnum.Ida || t == (int)ReserveTypeIdEnum.IdaVuelta)
            .WithMessage("ReserveTypeId must be Ida (1) or IdaVuelta (2).");
        RuleFor(x => x.PassengerCount).GreaterThan(0);
        RuleFor(x => x.ReserveDate).NotEmpty();
    }
}
