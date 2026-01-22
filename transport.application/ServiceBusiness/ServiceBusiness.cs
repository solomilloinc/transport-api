using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Cities;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Services.Abstraction;
using Transport.Domain.Trips;
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
        var trip = await _context.Trips.FindAsync(requestDto.TripId);
        if (trip is null)
            return Result.Failure<int>(TripError.TripNotFound);

        if (trip.Status != EntityStatusEnum.Active)
            return Result.Failure<int>(TripError.TripNotActive);

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
            TripId = requestDto.TripId,
            OriginId = requestDto.OriginId,
            DestinationId = requestDto.DestinationId,
            EstimatedDuration = requestDto.EstimatedDuration,
            VehicleId = requestDto.VehicleId,
            Status = EntityStatusEnum.Active
        };

        if (requestDto.Schedules?.Any() == true)
        {
            foreach (var scheduleDto in requestDto.Schedules)
            {
                var schedule = new ServiceSchedule
                {
                    ServiceId = service.ServiceId,
                    DepartureHour = scheduleDto.DepartureHour,
                    IsHoliday = scheduleDto.IsHoliday,
                    Status = EntityStatusEnum.Active
                };

                service.Schedules.Add(schedule);
            }
        }

        await _context.Services.AddAsync(service);
        await _context.SaveChangesWithOutboxAsync();

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
                s.StartDay,
                s.EndDay,
                new ServiceVehicleResponseDto(s.VehicleId,
                    s.Vehicle.InternalNumber,
                    s.Vehicle.AvailableQuantity,
                    s.Vehicle.VehicleType.Quantity,
                    s.Vehicle.VehicleType.Name,
                    s.Vehicle.VehicleType.ImageBase64),
                s.Status.ToString(),
                s.Schedules.Select(sc => new ServiceScheduleReportResponseDto(
                    sc.ServiceScheduleId,
                    sc.ServiceId,
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
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId);

        if (service == null)
            return Result.Failure<bool>(ServiceError.ServiceNotFound);

        var trip = await _context.Trips.FindAsync(dto.TripId);
        if (trip is null)
            return Result.Failure<bool>(TripError.TripNotFound);

        if (trip.Status != EntityStatusEnum.Active)
            return Result.Failure<bool>(TripError.TripNotActive);

        service.Name = dto.Name;
        service.TripId = dto.TripId;
        service.OriginId = dto.OriginId;
        service.DestinationId = dto.DestinationId;
        service.EstimatedDuration = dto.EstimatedDuration;
        service.VehicleId = dto.VehicleId;

        if (dto.Schedules?.Any() == true)
        {
            _context.ServiceSchedules.RemoveRange(service.Schedules);

            foreach (var scheduleDto in dto.Schedules)
            {
                var newSchedule = new ServiceSchedule
                {
                    ServiceId = serviceId,
                    DepartureHour = scheduleDto.DepartureHour,
                    IsHoliday = scheduleDto.IsHoliday,
                    Status = EntityStatusEnum.Active,
                };

                _context.ServiceSchedules.Add(newSchedule);
            }
        }

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
            .Where(s => s.Status == EntityStatusEnum.Active)
            .Include(s => s.Trip)
            .Include(s => s.Reserves.Where(r => r.Status != ReserveStatusEnum.Expired))
            .Include(s => s.Schedules.Where(sc => sc.Status == EntityStatusEnum.Active))
            .Include(s => s.Origin)
            .Include(s => s.Destination)
            .ToListAsync();

        foreach (var service in services)
        {
            // Skip services with inactive trip
            if (service.Trip.Status != EntityStatusEnum.Active)
                continue;

            foreach (var schedule in service.Schedules)
            {
                for (var date = today; date <= endDate; date = date.AddDays(1))
                {
                    if (!service.IsDayWithinScheduleRange(date.DayOfWeek))
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
                        EstimatedDuration = service.EstimatedDuration,
                        IsHoliday = schedule.IsHoliday,
                        ServiceName = service.Name,
                        TripId = service.TripId,
                        OriginId = service.OriginId,
                        DestinationId = service.DestinationId,
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

            _context.Reserves.Update(reserve);
        }

        await _context.SaveChangesWithOutboxAsync();
    }


    private bool IsHoliday(DateTime date)
    {
        return _context.Holidays.Any(h => h.HolidayDate == date.Date);
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

        var schedule = new ServiceSchedule
        {
            ServiceId = serviceId,
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
