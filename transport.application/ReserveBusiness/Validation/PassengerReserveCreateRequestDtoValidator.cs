using FluentValidation;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

public class PassengerReserveCreateRequestDtoValidator : AbstractValidator<PassengerReserveCreateRequestDto>
{
    public PassengerReserveCreateRequestDtoValidator()
    {
        RuleFor(x => x.ReserveId)
            .GreaterThan(0)
            .WithMessage("El ID de reserva debe ser mayor a 0");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("El precio debe ser mayor a 0");

        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("El ID de cliente debe ser mayor a 0");
    }
}
