using FluentValidation;
using Transport.SharedKernel.Contracts.Customer;
using Transport.Domain.Reserves;

namespace Transport.Business.ReserveBusiness.Validation;

public class CreatePaymentExternalRequestValidator : AbstractValidator<CreatePaymentExternalRequestDto>
{
    public CreatePaymentExternalRequestValidator()
    {
        RuleFor(x => x.TransactionAmount)
            .GreaterThan(0).WithMessage("The amount must be greater than zero.");

        RuleFor(x => x.PaymentMethodId).NotEmpty()
            .WithMessage("The payment method ID cannot be empty.");

        RuleFor(x => x.Installments)
            .GreaterThan(0).WithMessage("The Installments must be greater than zero.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("The token cannot be empty.");

        RuleFor(x => x.PayerEmail)
            .NotEmpty().WithMessage("The payer email cannot be empty.")
            .EmailAddress().WithMessage("The payer email must be a valid email address.");

        RuleFor(x => x.ReserveTypeId)
            .Must(value => Enum.IsDefined(typeof(ReserveTypeIdEnum), value))
            .WithMessage("The ReserveTypeId is not valid. Allowed values are 1 (Ida) and 2 (IdaVuelta).");

        RuleFor(x => x.IdentificationType)
            .NotEmpty().WithMessage("The identification type cannot be empty.");

        RuleFor(x => x.IdentificationNumber)
            .NotEmpty().WithMessage("The identification number cannot be empty.");
    }
}
