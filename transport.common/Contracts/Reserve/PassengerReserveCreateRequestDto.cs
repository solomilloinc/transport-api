namespace Transport.SharedKernel.Contracts.Reserve;

public record PassengerReserveCreateRequestDto(int ReserveId,
    int ReserveTypeId,
    int CustomerId,
    bool IsPayment,
    int? PickupLocationId,
    int? DropoffLocationId,
    bool HasTraveled,
    decimal Price);