using FluentValidation;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness.Validation;

public class ReservePriceCreateRequestValidator : AbstractValidator<ReservePriceCreateRequestDto>
{
    public ReservePriceCreateRequestValidator()
    {
        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than 0.");

        RuleFor(x => x.ReserveTypeId)
            .Must(value => Enum.IsDefined(typeof(ReserveTypeIdEnum), value))
            .WithMessage("Invalid ReserveTypeId value.");
    }
}
