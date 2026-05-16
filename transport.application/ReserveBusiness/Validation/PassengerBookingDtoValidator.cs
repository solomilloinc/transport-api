using FluentValidation;
using Transport.SharedKernel.Contracts.Passenger;

namespace Transport.Business.ReserveBusiness.Validation;

internal class PassengerBookingDtoValidator : AbstractValidator<PassengerBookingDto>
{
    public PassengerBookingDtoValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("CustomerId es obligatorio.");
        RuleFor(x => x.Outbound).NotNull().SetValidator(new LegInfoDtoValidator());
        RuleFor(x => x.Return).SetValidator(new LegInfoDtoValidator()!).When(x => x.Return is not null);
    }
}

internal class LegInfoDtoValidator : AbstractValidator<LegInfoDto>
{
    public LegInfoDtoValidator()
    {
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.");
    }
}
