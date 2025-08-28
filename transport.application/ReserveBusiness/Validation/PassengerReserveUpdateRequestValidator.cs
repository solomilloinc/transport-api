using FluentValidation;
using Transport.SharedKernel.Contracts.Passenger;

namespace Transport.Business.ReserveBusiness.Validation;

public class PassengerReserveUpdateRequestValidator : AbstractValidator<PassengerReserveUpdateRequestDto>
{
    public PassengerReserveUpdateRequestValidator()
    {
        RuleFor(x => x)
            .Must(x =>
                !(x.PickupLocationId.HasValue &&
                  x.DropoffLocationId.HasValue &&
                  x.PickupLocationId == x.DropoffLocationId))
            .WithMessage("Pickup and dropoff locations cannot be the same.");
    }
}
