using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Cities;
using Transport.Domain.Customers;
using Transport.Domain.Drivers;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Services.Abstraction;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Configuration;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness;

public class ServiceBusiness : IServiceBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly IReserveOption _reserveOption;
    private readonly IDateTimeProvider dateTimeProvider;

    public ServiceBusiness(IApplicationDbContext context, IReserveOption reserveOption, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _reserveOption = reserveOption;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<int>> Create(ServiceCreateRequestDto requestDto)
    {
        Vehicle vehicle = await _context.Vehicles.FindAsync(requestDto.VehicleId);

        if (vehicle == null)
        {
            return Result.Failure<int>(VehicleError.VehicleNotFound);
        }

        if (vehicle.Status != EntityStatusEnum.Active)
        {
            return Result.Failure<int>(VehicleError.VehicleNotAvailable);
        }

        City origin = await _context.Cities.FindAsync(requestDto.OriginId);

        if (origin == null)
        {
            return Result.Failure<int>(CityError.CityNotFound);
        }

        City destination = await _context.Cities.FindAsync(requestDto.DestinationId);

        if (destination == null)
        {
            return Result.Failure<int>(CityError.CityNotFound);
        }

        if (requestDto.StartDay > requestDto.EndDay)
        {
            return Result.Failure<int>(ServiceError.InvalidDayRange);
        }

        Service service = new Service
        {
            Name = requestDto.Name,
            StartDay = (DayOfWeek)requestDto.StartDay,
            EndDay = (DayOfWeek)requestDto.EndDay,
            OriginId = requestDto.OriginId,
            DestinationId = requestDto.DestinationId,
            EstimatedDuration = requestDto.EstimatedDuration,
            DepartureHour = requestDto.DepartureHour,
            IsHoliday = requestDto.IsHoliday,
            VehicleId = requestDto.VehicleId,
        };

        _context.Services.Add(service);
        await _context.SaveChangesWithOutboxAsync();

        return service.ServiceId;
    }

    public async Task<Result<PagedReportResponseDto<ServiceReportResponseDto>>>
        GetServiceReport(PagedReportRequestDto<ServiceReportFilterRequestDto> requestDto)
    {
        var query = _context.Services
            .AsNoTracking()
            .Include(s => s.Origin)
            .Include(s => s.Destination)
            .Include(s => s.Vehicle)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.Name))
            query = query.Where(s => s.Name.Contains(requestDto.Filters.Name));

        if (requestDto.Filters?.OriginId is not null && requestDto.Filters.OriginId > 0)
            query = query.Where(s => s.OriginId == requestDto.Filters.OriginId);

        if (requestDto.Filters?.DestinationId is not null && requestDto.Filters.DestinationId > 0)
            query = query.Where(s => s.DestinationId == requestDto.Filters.DestinationId);

        if (requestDto.Filters?.VehicleId is not null && requestDto.Filters.VehicleId > 0)
            query = query.Where(s => s.VehicleId == requestDto.Filters.VehicleId);

        if (requestDto.Filters?.IsHoliday is not null)
            query = query.Where(s => s.IsHoliday == requestDto.Filters.IsHoliday);

        if (requestDto.Filters?.Status is not null)
            query = query.Where(s => s.Status == requestDto.Filters.Status);

        var sortMappings = new Dictionary<string, Expression<Func<Service, object>>>
        {
            ["name"] = s => s.Name,
            ["originid"] = s => s.OriginId,
            ["destinationid"] = s => s.DestinationId,
            ["vehicleid"] = s => s.VehicleId,
            ["isholiday"] = s => s.IsHoliday,
            ["status"] = s => s.Status
        };

        var pagedResult = await query.ToPagedReportAsync<ServiceReportResponseDto, Service, ServiceReportFilterRequestDto>(
            requestDto,
            selector: s => new ServiceReportResponseDto(
                s.ServiceId,
                s.Name,
                s.OriginId,
                s.Origin.Name,
                s.DestinationId,
                s.Destination.Name,
                s.EstimatedDuration,
                s.DepartureHour,
                s.StartDay,
                s.EndDay,
                s.IsHoliday,
                new ServiceVehicleResponseDto(s.VehicleId,
                    s.Vehicle.InternalNumber,
                    s.Vehicle.AvailableQuantity,
                    s.Vehicle.VehicleType.Quantity,
                    s.Vehicle.VehicleType.Name,
                    s.Vehicle.VehicleType.ImageBase64),
                s.Status.ToString(),
                s.ReservePrices.Select(p => new ReservePriceReport((int)p.ReserveTypeId, p.Price)).ToList()
            ),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> Update(int serviceId, ServiceCreateRequestDto dto)
    {
        var service = await _context.Services
            .Include(s => s.ReservePrices)
            .FirstOrDefaultAsync(s => s.ServiceId == serviceId);

        if (service == null)
            return Result.Failure<bool>(ServiceError.ServiceNotFound);

        service.Name = dto.Name;
        service.StartDay = (DayOfWeek)dto.StartDay;
        service.EndDay = (DayOfWeek)dto.EndDay;
        service.OriginId = dto.OriginId;
        service.DestinationId = dto.DestinationId;
        service.EstimatedDuration = dto.EstimatedDuration;
        service.DepartureHour = dto.DepartureHour;
        service.IsHoliday = dto.IsHoliday;
        service.VehicleId = dto.VehicleId;

        _context.Services.Update(service);

        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }



    public async Task<Result<bool>> Delete(int serviceId)
    {
        var service = await _context.Services.FindAsync(serviceId);

        if (service == null)
            return Result.Failure<bool>(ServiceError.ServiceNotFound);

        service.Status = EntityStatusEnum.Deleted;

        _context.Services.Update(service);

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateStatus(int serviceId, EntityStatusEnum status)
    {
        var service = await _context.Services.FindAsync(serviceId);

        if (service == null)
            return Result.Failure<bool>(ServiceError.ServiceNotFound);

        service.Status = status;

        _context.Services.Update(service);

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> GenerateFutureReservesAsync()
    {
        await MarkOldReservesAsExpiredAsync();

        var today = dateTimeProvider.UtcNow;
        var endDate = today.AddDays(_reserveOption.ReserveGenerationDays);

        var services = await _context.Services
            .Where(s => s.Status == EntityStatusEnum.Active && s.ReservePrices.Any())
            .ToListAsync();

        foreach (var service in services)
        {
            var existingReserves = await _context.Reserves
                                  .Where(r =>
                                      r.ServiceId == service.ServiceId &&
                                      r.ReserveDate.Date >= today &&
                                      r.ReserveDate.Date <= endDate)
                                  .ToListAsync();

            for (var date = today; date <= endDate; date = date.AddDays(1))
            {
                if (!service.IsDayWithinServiceRange(service, date.DayOfWeek))
                    continue;

                if (IsHoliday(date) && !service.IsHoliday)
                    continue;

                var reserveDate = date.Date + service.DepartureHour;

                var existing = existingReserves.FirstOrDefault(r => r.ReserveDate.Date == date.Date);

                if (existing != null)
                {
                    if (existing.Status == ReserveStatusEnum.Expired)
                    {
                        existing.ReserveDate = reserveDate;
                        existing.Status = ReserveStatusEnum.Confirmed;
                    }

                    continue;
                }

                var newReserve = new Reserve
                {
                    ReserveDate = reserveDate,
                    ServiceId = service.ServiceId,
                    VehicleId = service.VehicleId,
                    Status = ReserveStatusEnum.Confirmed,
                };

                _context.Reserves.Add(newReserve);
            }
        }

        await _context.SaveChangesWithOutboxAsync();
        return true;
    }

    private async Task MarkOldReservesAsExpiredAsync()
    {
        var now = dateTimeProvider.UtcNow;

        var oldAvailableReserves = await _context.Reserves
            .Where(r => r.Status == ReserveStatusEnum.Available && r.ReserveDate < now)
            .ToListAsync();

        foreach (var reserve in oldAvailableReserves)
        {
            reserve.Status = ReserveStatusEnum.Expired;
        }

        await _context.SaveChangesWithOutboxAsync();
    }


    private bool IsHoliday(DateTime date)
    {
        return _context.Holidays.Any(h => h.HolidayDate == date.Date);
    }

    public async Task<Result<bool>> UpdatePricesByPercentageAsync(PriceMassiveUpdateRequestDto requestDto)
    {
        var services = await _context.Services
            .Include(s => s.ReservePrices)
            .Where(s => s.Status == EntityStatusEnum.Active && s.ReservePrices.Any())
            .ToListAsync();

        foreach (var service in services)
        {
            foreach (var priceUpdate in requestDto.PriceUpdates)
            {
                var matchingPrices = service.ReservePrices
                    .Where(p => p.ReserveTypeId == (ReserveTypeIdEnum)priceUpdate.ReserveTypeId && p.Status == EntityStatusEnum.Active);

                foreach (var price in matchingPrices)
                {
                    var originalPrice = price.Price;
                    var increase = originalPrice * (priceUpdate.Percentage / 100m);
                    price.Price = decimal.Round(originalPrice + increase, 2);
                }
            }

            _context.Services.Update(service);
        }

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> AddPrice(int serviceId, ServicePriceAddDto requestDto)
    {
        var service = await _context.Services.Include(p => p.ReservePrices).SingleOrDefaultAsync(p => p.ServiceId == serviceId);

        if (service is null)
        {
            return Result.Failure<bool>(ServiceError.ServiceNotFound);
        }

        if (service.ReservePrices.Any(p => p.ReserveTypeId == (ReserveTypeIdEnum)requestDto.ReserveTypeId))
        {
            return Result.Failure<bool>(ReservePriceError.ReservePriceAlreadyExists);
        }

        ReservePrice reservePrice = new ReservePrice
        {
            ServiceId = serviceId,
            ReserveTypeId = (ReserveTypeIdEnum)requestDto.ReserveTypeId,
            Price = requestDto.Price,
            Status = EntityStatusEnum.Active
        };

        _context.ReservePrices.Add(reservePrice);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdatePrice(int serviceId, ServicePriceUpdateDto requestDto)
    {
        ReservePrice reservePrice = await _context.ReservePrices.FindAsync(requestDto.ReservePriceId);

        if (reservePrice is null)
        {
            return Result.Failure<bool>(ReservePriceError.ReservePriceNotFound);
        }

        var service = await _context.Services.Include(p => p.ReservePrices).SingleOrDefaultAsync(p => p.ServiceId == serviceId);

        if (service is null)
        {
            return Result.Failure<bool>(ServiceError.ServiceNotFound);
        }

        reservePrice.Price = requestDto.Price;

        _context.ReservePrices.Update(reservePrice);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }
}
