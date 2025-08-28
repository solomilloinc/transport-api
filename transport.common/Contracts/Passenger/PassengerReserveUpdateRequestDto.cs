namespace Transport.SharedKernel.Contracts.Passenger;

public record PassengerReserveUpdateRequestDto(
        int? PickupLocationId,
        int? DropoffLocationId,
        bool? HasTraveled
    );