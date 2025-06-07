using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Cities;
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
        if (vehicle is null)
            return Result.Failure<int>(VehicleError.VehicleNotFound);

        if (vehicle.Status != EntityStatusEnum.Active)
            return Result.Failure<int>(VehicleError.VehicleNotAvailable);

        City origin = await _context.Cities.FindAsync(requestDto.OriginId);
        if (origin is null)
            return Result.Failure<int>(CityError.CityNotFound);

        City destination = await _context.Cities.FindAsync(requestDto.DestinationId);
        if (destination is null)
            return Result.Failure<int>(CityError.CityNotFound);

        var service = new Service
        {
            Name = requestDto.Name,
            OriginId = requestDto.OriginId,
            DestinationId = requestDto.DestinationId,
            EstimatedDuration = requestDto.EstimatedDuration,
            VehicleId = requestDto.VehicleId,
            Status = EntityStatusEnum.Active
        };

        _context.Services.Add(service);
        await _context.SaveChangesWithOutboxAsync();

        if (requestDto.Schedules?.Any() == true)
        {
            foreach (var scheduleDto in requestDto.Schedules)
            {
                if (scheduleDto.StartDay > scheduleDto.EndDay)
                    return Result.Failure<int>(ServiceError.InvalidDayRange);

                var schedule = new ServiceSchedule
                {
                    ServiceId = service.ServiceId,
                    StartDay = scheduleDto.StartDay,
                    EndDay = scheduleDto.EndDay,
                    DepartureHour = scheduleDto.DepartureHour,
                    IsHoliday = scheduleDto.IsHoliday,
                    Status = EntityStatusEnum.Active
                };

                _context.ServiceSchedules.Add(schedule);
            }

            await _context.SaveChangesWithOutboxAsync();
        }

        return Result.Success(service.ServiceId);
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

        if (requestDto.Filters?.Status is not null)
            query = query.Where(s => s.Status == requestDto.Filters.Status);

        var sortMappings = new Dictionary<string, Expression<Func<Service, object>>>
        {
            ["name"] = s => s.Name,
            ["originid"] = s => s.OriginId,
            ["destinationid"] = s => s.DestinationId,
            ["vehicleid"] = s => s.VehicleId,
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
                new ServiceVehicleResponseDto(s.VehicleId,
                    s.Vehicle.InternalNumber,
                    s.Vehicle.AvailableQuantity,
                    s.Vehicle.VehicleType.Quantity,
                    s.Vehicle.VehicleType.Name,
                    s.Vehicle.VehicleType.ImageBase64),
                s.Status.ToString(),
                s.ReservePrices.Select(p => new ReservePriceReport((int)p.ReserveTypeId, p.Price)).ToList(),
                s.Schedules.Select(sc => new ServiceScheduleReportResponseDto(
                    sc.ServiceScheduleId,
                    sc.ServiceId,
                    sc.StartDay,
                    sc.EndDay,
                    sc.DepartureHour,
                    sc.IsHoliday,
                    sc.Status.ToString()
                )).ToList()
            ),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> Update(int serviceId, ServiceCreateRequestDto dto)
    {
        var service = await _context.Services
            .Include(s => s.Schedules)
            .Include(s => s.ReservePrices)
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId);

        if (service == null)
            return Result.Failure<bool>(ServiceError.ServiceNotFound);

        service.Name = dto.Name;
        service.OriginId = dto.OriginId;
        service.DestinationId = dto.DestinationId;
        service.EstimatedDuration = dto.EstimatedDuration;
        service.VehicleId = dto.VehicleId;

        _context.Services.Update(service);

        if (dto.Schedules != null)
        {
            _context.ServiceSchedules.RemoveRange(service.Schedules);

            foreach (var scheduleDto in dto.Schedules)
            {
                if (scheduleDto.StartDay > scheduleDto.EndDay)
                    return Result.Failure<bool>(ServiceError.InvalidDayRange);

                var newSchedule = new ServiceSchedule
                {
                    ServiceId = serviceId,
                    StartDay = scheduleDto.StartDay,
                    EndDay = scheduleDto.EndDay,
                    DepartureHour = scheduleDto.DepartureHour,
                    IsHoliday = scheduleDto.IsHoliday,
                    Status = EntityStatusEnum.Active,
                };

                _context.ServiceSchedules.Add(newSchedule);
            }
        }

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
            .Include(p => p.Reserves.Where(p => p.Status != ReserveStatusEnum.Expired))
            .Include(s => s.Schedules.Where(p => p.Status == EntityStatusEnum.Active))
            .Include(s => s.Origin)
            .Include(s => s.Destination)
            .ToListAsync();

        foreach (var service in services)
        {
            foreach (var schedule in service.Schedules)
            {
                for (var date = today; date <= endDate; date = date.AddDays(1))
                {
                    if (!service.IsDayWithinScheduleRange(schedule, date.DayOfWeek))
                        continue;

                    if (IsHoliday(date) && !schedule.IsHoliday)
                        continue;

                    var fullReserveDate = date.Date + schedule.DepartureHour;

                    if (service.Reserves.Any(r => r.ReserveDate.Date == fullReserveDate.Date && r.ReserveDate.TimeOfDay == schedule.DepartureHour))
                        continue;

                    var reserve = new Reserve
                    {
                        ReserveDate = fullReserveDate,
                        ServiceId = service.ServiceId,
                        VehicleId = service.VehicleId,
                        Status = ReserveStatusEnum.Confirmed,
                        ServiceScheduleId = schedule.ServiceScheduleId,
                        DepartureHour = schedule.DepartureHour,
                        IsHoliday = schedule.IsHoliday,
                        ServiceName = service.Name,
                        OriginName = service.Origin.Name,
                        DestinationName = service.Destination.Name,                        
                    };

                    _context.Reserves.Add(reserve);
                }
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

    public async Task<Result<List<ServiceSchedule>>> GetSchedulesByServiceId(int serviceId)
    {
        var service = await _context.Services
            .Include(s => s.Schedules)
            .FirstOrDefaultAsync(s => s.ServiceId == serviceId);

        if (service is null)
            return Result.Failure<List<ServiceSchedule>>(ServiceError.ServiceNotFound);

        return Result.Success(service.Schedules.ToList());
    }

    public async Task<Result<int>> CreateSchedule(int serviceId, ServiceScheduleCreateDto request)
    {
        var service = await _context.Services.FindAsync(serviceId);
        if (service is null)
            return Result.Failure<int>(ServiceError.ServiceNotFound);

        if (request.StartDay > request.EndDay)
            return Result.Failure<int>(ServiceError.InvalidDayRange);

        var schedule = new ServiceSchedule
        {
            ServiceId = serviceId,
            StartDay = request.StartDay,
            EndDay = request.EndDay,
            DepartureHour = request.DepartureHour,
            IsHoliday = request.IsHoliday,
            Status = EntityStatusEnum.Active
        };

        _context.ServiceSchedules.Add(schedule);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(schedule.ServiceScheduleId);
    }

    public async Task<Result<bool>> UpdateSchedule(int scheduleId, ServiceScheduleUpdateDto request)
    {
        var schedule = await _context.ServiceSchedules.FindAsync(scheduleId);

        if (schedule is null)
            return Result.Failure<bool>(ServiceError.ServiceScheduleNotFound);

        if (request.StartDay > request.EndDay)
            return Result.Failure<bool>(ServiceError.InvalidDayRange);

        schedule.StartDay = request.StartDay;
        schedule.EndDay = request.EndDay;
        schedule.DepartureHour = request.DepartureHour;
        schedule.IsHoliday = request.IsHoliday;

        _context.ServiceSchedules.Update(schedule);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> DeleteSchedule(int scheduleId)
    {
        var schedule = await _context.ServiceSchedules.FindAsync(scheduleId);

        if (schedule is null)
            return Result.Failure<bool>(ServiceError.ServiceScheduleNotFound);

        schedule.Status = EntityStatusEnum.Deleted;

        _context.ServiceSchedules.Update(schedule);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateScheduleStatus(int scheduleId, EntityStatusEnum status)
    {
        var schedule = await _context.ServiceSchedules.FindAsync(scheduleId);

        if (schedule is null)
            return Result.Failure<bool>(ServiceError.ServiceScheduleNotFound);

        schedule.Status = status;

        _context.ServiceSchedules.Update(schedule);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }
}
