using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.Directions;
using Transport.Domain.Drivers;
using Transport.Domain.Reserves;
using Transport.Domain.Reserves.Abstraction;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Customer;
using Transport.SharedKernel.Contracts.Reserve;
using Transport.SharedKernel.Contracts.Service;

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

    public async Task<Result<bool>> CreatePassengerReserves(CustomerReserveCreateRequestWrapperDto customerReserves)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            foreach (var passenger in customerReserves.Items)
            {
                var reserve = await _context.Reserves
                    .Include(r => r.CustomerReserves)
                    .SingleOrDefaultAsync(r => r.ReserveId == passenger.reserveId);

                var service = await _context.Services
                    .Include(s => s.ReservePrices)
                    .Include(s => s.Origin)
                    .Include(s => s.Destination)
                    .Include(s => s.ReservePrices)
                    .SingleOrDefaultAsync(s => s.ServiceId == reserve.ServiceId);

                var vehicle = await _context.Vehicles
                    .FindAsync(reserve.VehicleId);

                if (reserve is null)
                    return Result.Failure<bool>(ReserveError.NotFound);

                if (reserve.Status != ReserveStatusEnum.Confirmed)
                    return Result.Failure<bool>(ReserveError.NotAvailable);

                var existingPassengerCount = reserve.CustomerReserves.Count;
                var newPassengerCount = customerReserves.Items.Count;
                var totalAfterInsert = existingPassengerCount + newPassengerCount;
                var vehicleCapacity = vehicle.AvailableQuantity;

                if (totalAfterInsert > vehicleCapacity)
                {
                    return Result.Failure<bool>(
                        ReserveError.VehicleQuantityNotAvailable(existingPassengerCount, newPassengerCount, vehicleCapacity)
                    );
                }

                var reservePrice = service.ReservePrices.Single(p => p.ReserveTypeId == (ReserveTypeIdEnum)passenger.ReserveTypeId);
                if (reservePrice is null)
                    return Result.Failure<bool>(ReserveError.PriceNotAvailable);

                if (passenger.price != reservePrice.Price)
                    return Result.Failure<bool>(ReserveError.PriceNotAvailable);


                var customerResult = await GetOrCreateCustomerAsync(passenger);
                if (!customerResult.IsSuccess)
                    return Result.Failure<bool>(customerResult.Error);

                var customer = customerResult.Value;

                if (reserve.CustomerReserves.Any(cr => cr.CustomerId == customer.CustomerId))
                    return Result.Failure<bool>(ReserveError.CustomerAlreadyExists(customer.DocumentNumber));

                var pickupResult = await GetDirectionAsync(passenger.PickupLocationId, "Pickup");
                if (pickupResult.IsFailure) return Result.Failure<bool>(pickupResult.Error);

                var dropoffResult = await GetDirectionAsync(passenger.DropoffLocationId, "Dropoff");
                if (dropoffResult.IsFailure) return Result.Failure<bool>(dropoffResult.Error);

                var pickUpDirection = pickupResult.Value;
                var dropOffDirection = dropoffResult.Value;

                var newCustomerReserve = new CustomerReserve
                {
                    Customer = customer,
                    ReserveId = reserve.ReserveId,
                    DropoffLocationId = passenger.DropoffLocationId,
                    PickupLocationId = passenger.PickupLocationId,
                    HasTraveled = passenger.HasTraveled,
                    Price = reservePrice.Price,
                    IsPayment = passenger.IsPayment,
                    StatusPayment = (StatusPaymentEnum)passenger.StatusPaymentId,
                    PaymentMethod = (PaymentMethodEnum)passenger.PaymentMethodId,
                    CustomerFullName = $"{customer.FirstName} {customer.LastName}",
                    DestinationCityName = service.Destination.Name,
                    OriginCityName = service.Origin.Name,
                    DriverName = reserve.Driver is not null ? $"{reserve.Driver?.FirstName} {reserve.Driver.LastName}" : null,
                    PickupAddress = pickUpDirection?.Name,
                    DropoffAddress = dropOffDirection?.Name,
                    ServiceName = service.Name,
                    VehicleInternalNumber = vehicle.InternalNumber,
                    CustomerEmail = customer.Email,
                    DocumentNumber = customer.DocumentNumber,
                    Phone1 = customer.Phone1,
                    Phone2 = customer.Phone2
                };

                reserve.CustomerReserves.Add(newCustomerReserve);

                reserve.Raise(new CustomerReserveCreatedEvent(newCustomerReserve.CustomerReserveId));

                _context.Reserves.Update(reserve);
            }

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    private async Task<Result<Direction?>> GetDirectionAsync(int? locationId, string type)
    {
        if (locationId is null)
            return Result.Success<Direction?>(null);

        var direction = await _context.Directions.FindAsync(locationId);

        if (direction is null)
        {
            return Result.Failure<Direction?>(
                Error.NotFound(
                    $"Direction.{type}NotFound",
                    $"{type} direction not found"
                )
            );
        }

        return Result.Success<Direction?>(direction);
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
        var query = _context.Reserves.Where(rp => rp.Status == ReserveStatusEnum.Confirmed);

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
                rp.OriginName,
                rp.DestinationName,
                rp.Service.Vehicle.AvailableQuantity,
                rp.CustomerReserves.Count,
                rp.DepartureHour.ToString(@"hh\:mm"),
                rp.CustomerReserves
                  .Select(p => new CustomerReserveReportResponseDto(
                      p.CustomerReserveId,
                      p.CustomerId,
                      $"{p.CustomerFullName}",
                      p.DocumentNumber,
                      p.CustomerEmail,
                      $"{p.Phone1} {p.Phone2}",
                      p.ReserveId,
                      p.DropoffLocationId!.Value,
                      p.PickupLocationId!.Value))
                  .ToList(),
                rp.Service.ReservePrices.Select(p => new ReservePriceReport((int)p.ReserveTypeId, p.Price)).ToList()
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
            .Where(cr => cr.ReserveId == reserveId);

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.CustomerFullName))
        {
            var nameFilter = requestDto.Filters.CustomerFullName.ToLower();
            query = query.Where(cr =>
                (cr.CustomerFullName).ToLower().Contains(nameFilter));
        }

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.DocumentNumber))
        {
            var docFilter = requestDto.Filters.DocumentNumber.Trim();
            query = query.Where(cr => cr.DocumentNumber.Contains(docFilter));
        }

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.Email))
        {
            var emailFilter = requestDto.Filters.Email.ToLower().Trim();
            query = query.Where(cr => cr.CustomerEmail.ToLower().Contains(emailFilter));
        }

        var sortMappings = new Dictionary<string, Expression<Func<CustomerReserve, object>>>
        {
            ["customerfullname"] = cr => cr.CustomerFullName,
            ["documentnumber"] = cr => cr.DocumentNumber,
        };

        var pagedResult = await query.ToPagedReportAsync<CustomerReserveReportResponseDto, CustomerReserve, CustomerReserveReportFilterRequestDto>(
            requestDto,
            selector: cr => new CustomerReserveReportResponseDto(
                cr.CustomerReserveId,
                cr.CustomerId,
                cr.CustomerFullName,
                cr.DocumentNumber,
                cr.CustomerEmail,
                $"{cr.Phone1} {cr.Phone2}",
                cr.ReserveId,
                cr.DropoffLocationId!.Value,
                cr.PickupLocationId!.Value),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> UpdateReserveAsync(int reserveId, ReserveUpdateRequestDto request)
    {
        var reserve = await _context.Reserves
            .FirstOrDefaultAsync(r => r.ReserveId == reserveId);

        if (reserve is null)
            return Result.Failure<bool>(ReserveError.NotFound);

        if (request.VehicleId != null)
        {
            var vehicle = await _context.Vehicles.FindAsync(request.VehicleId);
            if (vehicle is null)
                return Result.Failure<bool>(VehicleError.VehicleNotFound);
            reserve.VehicleId = request.VehicleId.Value;
        }

        if (request.DriverId != null)
        {
            var driver = await _context.Drivers.FindAsync(request.DriverId);
            if (driver is null)
                return Result.Failure<bool>(DriverError.DriverNotFound);
            reserve.DriverId = request.DriverId.Value;
        }

        reserve.ReserveDate = request.ReserveDate ?? reserve.ReserveDate;
        reserve.DepartureHour = request.DepartureHour ?? reserve.DepartureHour;

        if (request.Status != null)
        {
            reserve.Status = (ReserveStatusEnum)request.Status;
        }

        _context.Reserves.Update(reserve);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }
}
