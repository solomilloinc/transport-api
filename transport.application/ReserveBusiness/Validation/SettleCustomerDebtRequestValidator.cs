using FluentValidation;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

public class SettleCustomerDebtRequestValidator : AbstractValidator<SettleCustomerDebtRequestDto>
{
    public SettleCustomerDebtRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("CustomerId debe ser mayor a 0.");
        RuleFor(x => x.ReserveIds).NotEmpty().WithMessage("Debe especificar al menos una reserva.");
        RuleFor(x => x.Payments).NotEmpty().WithMessage("Debe proporcionar al menos un pago.");
        RuleForEach(x => x.Payments).SetValidator(new PaymentCreateRequestValidator());
    }
}
