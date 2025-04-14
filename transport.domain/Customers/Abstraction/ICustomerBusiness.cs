using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Customer.Reserve;

namespace Transport.Domain.Customers.Abstraction;

public interface ICustomerBusiness
{
    Task<Result<int>> CreateReserve(CustomerReserveCreateRequestDto dto);
}
