using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.SharedKernel.Contracts.Customer;

public record CustomerReserveCreateRequestWrapperDto(List<CustomerReserveCreateRequestDto> Items);

public record CustomerCreateRequestDto(string FirstName, 
    string LastName, 
    string Email, 
    string DocumentNumber, 
    string Phone1, 
    string? Phone2);
