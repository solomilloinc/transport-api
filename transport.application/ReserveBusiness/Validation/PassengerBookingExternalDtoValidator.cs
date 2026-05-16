using FluentValidation;
using Transport.SharedKernel.Contracts.Passenger;

namespace Transport.Business.ReserveBusiness.Validation;

internal class PassengerBookingExternalDtoValidator : AbstractValidator<PassengerBookingExternalDto>
{
    public PassengerBookingExternalDtoValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("FirstName es obligatorio.");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("LastName es obligatorio.");
        RuleFor(x => x.DocumentNumber).NotEmpty().WithMessage("DocumentNumber es obligatorio.");
        RuleFor(x => x.Phone1).NotEmpty().WithMessage("Phone1 es obligatorio.");
        RuleFor(x => x.Outbound).NotNull().SetValidator(new LegInfoDtoValidator());
        RuleFor(x => x.Return).SetValidator(new LegInfoDtoValidator()!).When(x => x.Return is not null);
    }
}
