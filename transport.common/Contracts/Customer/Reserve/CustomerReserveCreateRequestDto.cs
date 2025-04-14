namespace Transport.SharedKernel.Contracts.Customer.Reserve;

public record CustomerReserveCreateRequestDto(int CustomerId, int ReserveId, bool IsPayment, int PickupLocationId, int DropoffLocationId);
