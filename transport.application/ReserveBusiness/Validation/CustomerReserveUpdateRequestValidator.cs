using FluentValidation;
using Transport.SharedKernel.Contracts.Customer;

namespace Transport.Business.ReserveBusiness.Validation;

public class CustomerReserveUpdateRequestValidator : AbstractValidator<CustomerReserveUpdateRequestDto>
{
    public CustomerReserveUpdateRequestValidator()
    {
        RuleFor(x => x)
            .Must(x =>
                !(x.PickupLocationId.HasValue &&
                  x.DropoffLocationId.HasValue &&
                  x.PickupLocationId == x.DropoffLocationId))
            .WithMessage("Pickup and dropoff locations cannot be the same.");
    }
}
