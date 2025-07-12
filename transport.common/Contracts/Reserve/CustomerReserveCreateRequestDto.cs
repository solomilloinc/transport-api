using Transport.SharedKernel.Contracts.Customer;

namespace Transport.SharedKernel.Contracts.Reserve;

public record CustomerReserveCreateRequestDto(int ReserveId,
    int ReserveTypeId,
    int? CustomerId, 
    bool IsPayment, 
    int? PickupLocationId,
    int? DropoffLocationId,
    bool HasTraveled,
    decimal Price,
    CustomerCreateRequestDto? CustomerCreate);
