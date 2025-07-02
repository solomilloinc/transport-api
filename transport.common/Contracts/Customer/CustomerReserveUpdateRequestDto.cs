namespace Transport.SharedKernel.Contracts.Customer;

public record CustomerReserveUpdateRequestDto(
        int? PickupLocationId,
        int? DropoffLocationId,
        bool? HasTraveled
    );