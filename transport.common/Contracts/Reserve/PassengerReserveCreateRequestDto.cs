using Transport.SharedKernel.Contracts.Customer;

namespace Transport.SharedKernel.Contracts.Reserve;

public record PassengerReserveCreateRequestDto(int ReserveId,
    int ReserveTypeId,
    int? CustomerId, 
    bool IsPayment, 
    int? PickupLocationId,
    int? DropoffLocationId,
    bool HasTraveled,
    decimal Price,
    string FirstName,
    string LastName,
    string? Email,
    string Phone1,
    string DocumentNumber);
