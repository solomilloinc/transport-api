using FluentValidation;
using Transport.SharedKernel.Contracts.Payment;

namespace Transport.Business.PaymentBusiness.Validation;

public class PaymentCreateRequestValidator : AbstractValidator<PaymentCreateRequestDto>
{
    public PaymentCreateRequestValidator()
    {
        RuleFor(x => x.TransactionAmount)
            .GreaterThan(0).WithMessage("The transaction amount must be greater than 0.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("The token is required.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("The description is required.")
            .MaximumLength(255).WithMessage("The description must not exceed 255 characters.");

        RuleFor(x => x.Installments)
            .GreaterThan(0).WithMessage("Installments must be greater than 0.");

        RuleFor(x => x.PaymentMethodId)
            .NotEmpty().WithMessage("The payment method ID is required.");

        RuleFor(x => x.Payer)
            .NotNull().WithMessage("Payer information is required.");

        //RuleFor(x => x.Payer.Email)
        //    .NotEmpty().WithMessage("The payer email is required.")
        //    .EmailAddress().WithMessage("The payer email is not valid.");
    }
}
