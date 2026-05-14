namespace Transport.SharedKernel.Contracts.Passenger;

public record LegInfoDto(
    int? PickupLocationId,
    int? DropoffLocationId,
    decimal Price);
