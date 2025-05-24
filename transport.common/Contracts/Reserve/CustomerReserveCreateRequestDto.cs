using Transport.SharedKernel.Contracts.Customer;

namespace Transport.SharedKernel.Contracts.Reserve;

public record CustomerReserveCreateRequestDto(int? CustomerId, 
    bool IsPayment, 
    int? PickupLocationId,
    int? DropoffLocationId,
    bool HasTraveled,
    decimal price,
    int StatusPaymentId,
    CustomerCreateRequestDto? CustomerCreate);
