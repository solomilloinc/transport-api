using Microsoft.EntityFrameworkCore;
using Transport.SharedKernel;
using Transport.Business.Data;
using Transport.Domain.Customers.Abstraction;
using Transport.SharedKernel.Contracts.Customer.Reserve;
using Transport.Domain.Customers;

namespace Transport.Business.CustomerBusiness;

public class CustomerBusiness : ICustomerBusiness
{
    private readonly IApplicationDbContext _context;

    public CustomerBusiness(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> CreateReserve(CustomerReserveCreateRequestDto dto)
    {
        var customer = _context.Customers
            .SingleOrDefaultAsync(x => x.CustomerId == dto.CustomerId);

        if (customer is null)
        {
            return Result.Failure<int>(CustomerReserveError.CustomerNotFound);
        }

        var reserve = _context.Reserves
            .SingleOrDefaultAsync(x => x.ReserveId == dto.ReserveId);

        if (reserve is null)
        {
            return Result.Failure<int>(CustomerReserveError.CustomerNotFound);
        }

        var pickUpLocation = _context.Directions
            .SingleOrDefaultAsync(x => x.DirectionId == dto.PickupLocationId);

        if (pickUpLocation is null)
        {
            return Result.Failure<int>(CustomerReserveError.PickupLocationNotFound);
        }

        var dropOffLocation = _context.Directions
            .SingleOrDefaultAsync(x => x.DirectionId == dto.DropoffLocationId);

        if (dropOffLocation is null)
        {
            return Result.Failure<int>(CustomerReserveError.DropoffLocationNotFound);
        }

        //TODO: Logica del Price

        return null;
    }
}
