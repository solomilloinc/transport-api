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

    public ReserveBusiness(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> CreatePassengerReserves(List<CustomerReserveCreateRequestDto> customerReserves)
    {
        //var reserve = await _context.Reserves
        //    .Include(r => r.Service)
        //        .ThenInclude(s => s.ReservePrices.Where(p => p.ReserveTypeId == (ReserveTypeIdEnum)reserveTypeId))
        //    .Include(r => r.CustomerReserves)
        //    .SingleOrDefaultAsync(r => r.ReserveId == reserveId);

        //if (reserve is null)
        //    return Result.Failure<bool>(ReserveError.NotFound);

        //if (reserve.Status != ReserveStatusEnum.Available)
        //    return Result.Failure<bool>(ReserveError.NotAvailable);

        //var reservePrice = reserve.Service?.ReservePrices.FirstOrDefault();
        //if (reservePrice is null)
        //    return Result.Failure<bool>(ReserveError.PriceNotAvailable);

        //var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        //{
        //    foreach (var passenger in customerReserves)
        //    {
        //        var customerResult = await GetOrCreateCustomerAsync(passenger);
        //        if (!customerResult.IsSuccess)
        //            return Result.Failure<bool>(customerResult.Error);

        //        var customer = customerResult.Value;

        //        if (!reserve.CustomerReserves.Any(cr => cr.CustomerId == customer.CustomerId))
        //        {
        //            reserve.CustomerReserves.Add(new CustomerReserve
        //            {
        //                Customer = customer,
        //                ReserveId = reserve.ReserveId,
        //                DropoffLocationId = passenger.DropoffLocationId,
        //                PickupLocationId = passenger.PickupLocationId,
        //                HasTraveled = passenger.HasTraveled,
        //                Price = reservePrice.Price,
        //                IsPayment = passenger.IsPayment,
        //                StatusPayment = (StatusPaymentEnum)passenger.StatusPaymentId,
        //                PaymentMethod = (PaymentMethodEnum)passenger.PaymentMethodId
        //            });
        //        }
        //    }

        //    foreach (var cr in reserve.CustomerReserves)
        //        reserve.Raise(new CustomerReserveCreatedEvent(cr.CustomerReserveId));

        //    _context.Reserves.Update(reserve);
        //    await _context.SaveChangesWithOutboxAsync();

        //    return Result.Success(true);
        //});

        //return result;

        throw new NotImplementedException("This method is not implemented yet. Please implement it according to your business logic.");
    }


    private async Task<Result<Customer>> GetOrCreateCustomerAsync(CustomerReserveCreateRequestDto passenger)
    {
        if (passenger.CustomerId is null)
        {
            var existing = await _context.Customers
                .SingleOrDefaultAsync(c => c.DocumentNumber == passenger.CustomerCreate.DocumentNumber);

            if (existing != null)
            {
                return Result.Failure<Customer>(ReserveError.CustomerAlreadyExists(existing.DocumentNumber));
            }

            var newCustomer = new Customer
            {
                FirstName = passenger.CustomerCreate.FirstName,
                LastName = passenger.CustomerCreate.LastName,
                DocumentNumber = passenger.CustomerCreate.DocumentNumber,
                Email = passenger.CustomerCreate.Email,
                Phone1 = passenger.CustomerCreate.Phone1,
                Phone2 = passenger.CustomerCreate.Phone2
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

            return Result.Success(customer);
        }
    }


    public async Task<Result<PagedReportResponseDto<ReservePriceReportResponseDto>>>
    GetReservePriceReport(PagedReportRequestDto<ReservePriceReportFilterRequestDto> requestDto)
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

        if (requestDto.Filters?.PriceFrom is not null)
            query = query.Where(rp => rp.Price >= requestDto.Filters.PriceFrom);

        if (requestDto.Filters?.PriceTo is not null)
            query = query.Where(rp => rp.Price <= requestDto.Filters.PriceTo);

        var sortMappings = new Dictionary<string, Expression<Func<ReservePrice, object>>>
        {
            ["reservepriceid"] = rp => rp.ReservePriceId,
            ["serviceid"] = rp => rp.ServiceId,
            ["servicename"] = rp => rp.Service.Name,
            ["price"] = rp => rp.Price,
            ["reservetypeid"] = rp => rp.ReserveTypeId
        };

        var pagedResult = await query.ToPagedReportAsync<ReservePriceReportResponseDto, ReservePrice, ReservePriceReportFilterRequestDto>(
            requestDto,
            selector: rp => new ReservePriceReportResponseDto(
                rp.ReservePriceId,
                rp.ServiceId,
                rp.Service.Name,
                rp.Price,
                (int)rp.ReserveTypeId
            ),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }


    public async Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>> GetReserveReport(DateTime reserveDate,
    PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto)
    {
        var query = _context.Reserves
            .Include(rp => rp.Service).ThenInclude(s => s.Origin)
            .Include(rp => rp.Service).ThenInclude(s => s.Destination)
            .Include(rp => rp.Service).ThenInclude(s => s.Vehicle)
            .Include(rp => rp.CustomerReserves).ThenInclude(cr => cr.Customer)
            .Where(rp => rp.Status == ReserveStatusEnum.Available);


        var date = reserveDate.Date;
        query = query.Where(rp => rp.ReserveDate.Date == date);

        var sortMappings = new Dictionary<string, Expression<Func<Reserve, object>>>
        {
            ["reservedate"] = rp => rp.ReserveDate,
            ["serviceorigin"] = rp => rp.Service.Origin.Name,
            ["servicedest"] = rp => rp.Service.Destination.Name,
        };

        var pagedResult = await query.ToPagedReportAsync<ReserveReportResponseDto, Reserve, ReserveReportFilterRequestDto>(
            requestDto,
            selector: rp => new ReserveReportResponseDto(
                rp.ReserveId,
                rp.Service.Origin.Name,
                rp.Service.Destination.Name,
                rp.Service.Vehicle.AvailableQuantity,
                rp.CustomerReserves.Count,
                rp.Service.DepartureHour,
                rp.CustomerReserves
                  .Select(p => new CustomerReserveReportResponseDto(
                      p.CustomerReserveId,
                      p.CustomerId,
                      $"{p.Customer.FirstName} {p.Customer.LastName}",
                      p.Customer.DocumentNumber,
                      p.Customer.Email,
                      $"{p.Customer.Phone1} {p.Customer.Phone1}",
                      p.ReserveId,
                      p.DropoffLocationId!.Value,
                      p.PickupLocationId!.Value))
                  .ToList()
            ),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<PagedReportResponseDto<CustomerReserveReportResponseDto>>> GetReserveCustomerReport(
    int reserveId,
    PagedReportRequestDto<CustomerReserveReportFilterRequestDto> requestDto)
    {
        var query = _context.CustomerReserves
            .Include(cr => cr.Customer)
            .Where(cr => cr.ReserveId == reserveId);

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.CustomerFullName))
        {
            var nameFilter = requestDto.Filters.CustomerFullName.ToLower();
            query = query.Where(cr =>
                (cr.Customer.FirstName + " " + cr.Customer.LastName).ToLower().Contains(nameFilter));
        }

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.DocumentNumber))
        {
            var docFilter = requestDto.Filters.DocumentNumber.Trim();
            query = query.Where(cr => cr.Customer.DocumentNumber.Contains(docFilter));
        }

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.Email))
        {
            var emailFilter = requestDto.Filters.Email.ToLower().Trim();
            query = query.Where(cr => cr.Customer.Email.ToLower().Contains(emailFilter));
        }

        var sortMappings = new Dictionary<string, Expression<Func<CustomerReserve, object>>>
        {
            ["firstname"] = cr => cr.Customer.FirstName,
            ["lastname"] = cr => cr.Customer.LastName,
            ["documentnumber"] = cr => cr.Customer.DocumentNumber,
        };

        var pagedResult = await query.ToPagedReportAsync<CustomerReserveReportResponseDto, CustomerReserve, CustomerReserveReportFilterRequestDto>(
            requestDto,
            selector: cr => new CustomerReserveReportResponseDto(
                cr.CustomerReserveId,
                cr.CustomerId,
                $"{cr.Customer.FirstName} {cr.Customer.LastName}",
                cr.Customer.DocumentNumber,
                cr.Customer.Email,
                $"{cr.Customer.Phone1} {cr.Customer.Phone2}",
                cr.ReserveId,
                cr.DropoffLocationId!.Value,
                cr.PickupLocationId!.Value),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

}
