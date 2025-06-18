using FluentValidation;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Customer;

namespace Transport.Business.ReserveBusiness.Validation;

public class PaymentCreateRequestValidator : AbstractValidator<CreatePaymentRequestDto>
{
    public PaymentCreateRequestValidator()
    {
        RuleFor(x => x.PaymentMethod)
         .Must(value => Enum.IsDefined(typeof(PaymentMethodEnum), value))
         .WithMessage("El tipo de medio de pago es inválido");

        RuleFor(x => x.TransactionAmount).GreaterThan(0).WithMessage("La transacciòn debe ser vàlida");
    }
}
