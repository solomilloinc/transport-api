using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.Reserves;
using Transport.Domain.Reserves.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness;

public class ReserveBusiness : IReserveBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> CreatePassengerReserves(int reserveId,
    int reserveTypeId,
    List<CustomerReserveCreateRequestDto> passengers)
    {
        var reserve = await _context.Reserves
            .Include(r => r.Service)
                .ThenInclude(s => s.ReservePrices.Where(p => p.ReserveTypeId == (ReserveTypeIdEnum)reserveTypeId))
            .Include(r => r.CustomerReserves)
            .SingleOrDefaultAsync(r => r.ReserveId == reserveId);

        if (reserve is null)
            return Result.Failure<bool>(ReserveError.NotFound);

        if (reserve.Status != ReserveStatusEnum.Available)
            return Result.Failure<bool>(ReserveError.NotAvailable);

        var reservePrice = reserve.Service?.ReservePrices.FirstOrDefault();
        if (reservePrice is null)
            return Result.Failure<bool>(ReserveError.PriceNotAvailable);

        var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            foreach (var passenger in passengers)
            {
                var customerResult = await GetOrCreateCustomerAsync(passenger);
                if (!customerResult.IsSuccess)
                    return Result.Failure<bool>(customerResult.Error);

                var customer = customerResult.Value;

                if (!reserve.CustomerReserves.Any(cr => cr.CustomerId == customer.CustomerId))
    public async Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>>
     GetReserveReport(PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto)
    {
        var query = _context.ReservePrices
            .AsNoTracking()
            .Include(rp => rp.Service)
            .Where(rp => rp.Status == EntityStatusEnum.Active)
            .AsQueryable();

        if (requestDto.Filters?.ReserveTypeId is not null)
            query = query.Where(rp => (int)rp.ReserveTypeId == requestDto.Filters.ReserveTypeId);

        if (requestDto.Filters?.ServiceId is not null)
            query = query.Where(rp => rp.ServiceId == requestDto.Filters.ServiceId);

    private async Task<Result<Customer>> GetOrCreateCustomerAsync(CustomerReserveCreateRequestDto passenger)
    {
        if (passenger.CustomerId is null)
        {
            var existing = await _context.Customers
                .SingleOrDefaultAsync(c => c.DocumentNumber == passenger.CustomerCreate.DocumentNumber);
        if (requestDto.Filters?.PriceFrom is not null)
            query = query.Where(rp => rp.Price >= requestDto.Filters.PriceFrom);

            if (existing != null)
            {
                return Result.Failure<Customer>(ReserveError.CustomerAlreadyExists(existing.DocumentNumber));
            }
        if (requestDto.Filters?.PriceTo is not null)
            query = query.Where(rp => rp.Price <= requestDto.Filters.PriceTo);

            var newCustomer = new Customer
        var sortMappings = new Dictionary<string, Expression<Func<ReservePrice, object>>>
        {
                FirstName = passenger.CustomerCreate.FirstName,
                LastName = passenger.CustomerCreate.LastName,
                DocumentNumber = passenger.CustomerCreate.DocumentNumber,
                Email = passenger.CustomerCreate.Email,
                Phone1 = passenger.CustomerCreate.Phone1,
                Phone2 = passenger.CustomerCreate.Phone2
            ["reservepriceid"] = rp => rp.ReservePriceId,
            ["serviceid"] = rp => rp.ServiceId,
            ["servicename"] = rp => rp.Service.Name,
            ["price"] = rp => rp.Price,
            ["reservetypeid"] = rp => rp.ReserveTypeId
        };

            _context.Customers.Add(newCustomer);
            await _context.SaveChangesWithOutboxAsync();

            return Result.Success(newCustomer);
        }
        else
        {
            var customer = await _context.Customers.FindAsync(passenger.CustomerId);
            if (customer is null)
            {
                return Result.Failure<Customer>(CustomerError.NotFound);
            }

            if (customer.Status != EntityStatusEnum.Active)
            {
                return Result.Failure<Customer>(ReserveError.CustomerAlreadyExists(customer.DocumentNumber));
            }
        var pagedResult = await query.ToPagedReportAsync<ReserveReportResponseDto, ReservePrice, ReserveReportFilterRequestDto>(
            requestDto,
            selector: rp => new ReserveReportResponseDto(
                rp.ReservePriceId,
                rp.ServiceId,
                rp.Service.Name,
                rp.Price,
                (int)rp.ReserveTypeId
            ),
            sortMappings: sortMappings
        );

            return Result.Success(customer);
        }
    }

}
