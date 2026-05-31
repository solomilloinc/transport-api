using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Authentication;
using Transport.Business.Data;
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
    private readonly ITenantContext _tenantContext;

    public ServiceBusiness(
        IApplicationDbContext context,
        IReserveOption reserveOption,
        IDateTimeProvider dateTimeProvider,
        ITenantContext tenantContext)
    {
        _context = context;
        _reserveOption = reserveOption;
        this.dateTimeProvider = dateTimeProvider;
        _tenantContext = tenantContext;
    }

    public async Task<Result<int>> Create(ServiceCreateRequestDto requestDto)
    {
        var trip = await _context.Trips.Where(x => x.TripId == requestDto.TripId).FirstOrDefaultAsync();
        if (trip is null)
            return Result.Failure<int>(TripError.TripNotFound);

        if (trip.Status != EntityStatusEnum.Active)
            return Result.Failure<int>(TripError.TripNotActive);

        Vehicle vehicle = await _context.Vehicles.Where(x => x.VehicleId == requestDto.VehicleId).FirstOrDefaultAsync();
        if (vehicle is null)
            return Result.Failure<int>(VehicleError.VehicleNotFound);

        if (vehicle.Status != EntityStatusEnum.Active)
            return Result.Failure<int>(VehicleError.VehicleNotAvailable);

        var slotConflict = await _context.Services.AnyAsync(s =>
            s.TripId == requestDto.TripId &&
            s.DayOfWeek == requestDto.DayOfWeek &&
            s.DepartureHour == requestDto.DepartureHour &&
            s.Status == EntityStatusEnum.Active);

        if (slotConflict)
            return Result.Failure<int>(ServiceError.SlotConflict(requestDto.TripId, requestDto.DayOfWeek, requestDto.DepartureHour));

        var service = new Service
        {
            Name = requestDto.Name,
            TripId = requestDto.TripId,
            VehicleId = requestDto.VehicleId,
            DayOfWeek = requestDto.DayOfWeek,
            DepartureHour = requestDto.DepartureHour,
            EstimatedDuration = requestDto.EstimatedDuration,
            IsHoliday = requestDto.IsHoliday,
            Status = EntityStatusEnum.Active
        };

        if (requestDto.AllowedDirectionIds?.Any() == true)
        {
            foreach (var directionId in requestDto.AllowedDirectionIds.Distinct())
            {
                service.AllowedDirections.Add(new ServiceDirection
                {
                    DirectionId = directionId
                });
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
            .Include(s => s.Trip.OriginCity)
            .Include(s => s.Trip.DestinationCity)
            .Include(s => s.Vehicle)
            .Include(s => s.AllowedDirections)
                .ThenInclude(ad => ad.Direction)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.Name))
            query = query.Where(s => s.Name.Contains(requestDto.Filters.Name));

        if (requestDto.Filters?.OriginId is not null && requestDto.Filters.OriginId > 0)
            query = query.Where(s => s.Trip.OriginCityId == requestDto.Filters.OriginId);

        if (requestDto.Filters?.DestinationId is not null && requestDto.Filters.DestinationId > 0)
            query = query.Where(s => s.Trip.DestinationCityId == requestDto.Filters.DestinationId);

        if (requestDto.Filters?.VehicleId is not null && requestDto.Filters.VehicleId > 0)
            query = query.Where(s => s.VehicleId == requestDto.Filters.VehicleId);

        if (requestDto.Filters?.Status is not null)
            query = query.Where(s => s.Status == requestDto.Filters.Status);
        else
            query = query.Where(s => s.Status == EntityStatusEnum.Active);

        var sortMappings = new Dictionary<string, Expression<Func<Service, object>>>
        {
            ["name"] = s => s.Name,
            ["originid"] = s => s.Trip.OriginCityId,
            ["destinationid"] = s => s.Trip.DestinationCityId,
            ["vehicleid"] = s => s.VehicleId,
            ["dayofweek"] = s => s.DayOfWeek,
            ["departurehour"] = s => s.DepartureHour,
            ["status"] = s => s.Status
        };

        var pagedResult = await query.ToPagedReportAsync<ServiceReportResponseDto, Service, ServiceReportFilterRequestDto>(
            requestDto,
            selector: s => new ServiceReportResponseDto(
                s.ServiceId,
                s.Name,
                s.TripId,
                s.Trip.Description,
                s.Trip.OriginCityId,
                s.Trip.OriginCity.Name,
                s.Trip.DestinationCityId,
                s.Trip.DestinationCity.Name,
                s.DayOfWeek,
                s.DepartureHour,
                s.EstimatedDuration,
                s.IsHoliday,
                new ServiceVehicleResponseDto(s.VehicleId,
                    s.Vehicle.InternalNumber,
                    s.Vehicle.AvailableQuantity,
                    s.Vehicle.VehicleType.Quantity,
                    s.Vehicle.VehicleType.Name,
                    s.Vehicle.VehicleType.ImageBase64),
                s.Status.ToString(),
                s.AllowedDirections.Select(ad => new ServiceDirectionResponseDto(
                    ad.DirectionId,
                    ad.Direction.Name,
                    ad.Direction.CityId
                )).ToList()
            ),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> Update(int serviceId, ServiceCreateRequestDto dto)
    {
        var service = await _context.Services
            .Include(s => s.AllowedDirections)
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId);

        if (service == null)
            return Result.Failure<bool>(ServiceError.ServiceNotFound);

        var trip = await _context.Trips.Where(x => x.TripId == dto.TripId).FirstOrDefaultAsync();
        if (trip is null)
            return Result.Failure<bool>(TripError.TripNotFound);

        if (trip.Status != EntityStatusEnum.Active)
            return Result.Failure<bool>(TripError.TripNotActive);

        var slotConflict = await _context.Services.AnyAsync(s =>
            s.ServiceId != serviceId &&
            s.TripId == dto.TripId &&
            s.DayOfWeek == dto.DayOfWeek &&
            s.DepartureHour == dto.DepartureHour &&
            s.Status == EntityStatusEnum.Active);

        if (slotConflict)
            return Result.Failure<bool>(ServiceError.SlotConflict(dto.TripId, dto.DayOfWeek, dto.DepartureHour));

        if (dto.VehicleId != service.VehicleId)
        {
            var newVehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == dto.VehicleId);
            if (newVehicle is null)
                return Result.Failure<bool>(VehicleError.VehicleNotFound);
            if (newVehicle.Status != EntityStatusEnum.Active)
                return Result.Failure<bool>(VehicleError.VehicleNotAvailable);

            var subsCount = await CountActiveSubscriptionsConsumingService(serviceId);
            if (subsCount > newVehicle.AvailableQuantity)
                return Result.Failure<bool>(ServiceError.VehicleCapacityBelowSubscriptions(serviceId, newVehicle.AvailableQuantity, subsCount));
        }

        service.Name = dto.Name;
        service.TripId = dto.TripId;
        service.VehicleId = dto.VehicleId;
        service.DayOfWeek = dto.DayOfWeek;
        service.DepartureHour = dto.DepartureHour;
        service.EstimatedDuration = dto.EstimatedDuration;
        service.IsHoliday = dto.IsHoliday;

        if (dto.AllowedDirectionIds is not null)
        {
            _context.ServiceDirections.RemoveRange(service.AllowedDirections);

            foreach (var directionId in dto.AllowedDirectionIds.Distinct())
            {
                _context.ServiceDirections.Add(new ServiceDirection
                {
                    ServiceId = serviceId,
                    DirectionId = directionId
                });
            }
        }

        _context.Services.Update(service);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }


    public async Task<Result<bool>> Delete(int serviceId)
    {
        var service = await _context.Services.Where(x => x.ServiceId == serviceId).FirstOrDefaultAsync();

        if (service == null)
            return Result.Failure<bool>(ServiceError.ServiceNotFound);

        var subsCount = await CountActiveSubscriptionsConsumingService(serviceId);
        if (subsCount > 0)
            return Result.Failure<bool>(ServiceError.HasActiveSubscriptions(serviceId, subsCount));

        service.Status = EntityStatusEnum.Deleted;

        _context.Services.Update(service);

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateStatus(int serviceId, EntityStatusEnum status)
    {
        var service = await _context.Services.Where(x => x.ServiceId == serviceId).FirstOrDefaultAsync();

        if (service == null)
            return Result.Failure<bool>(ServiceError.ServiceNotFound);

        if (status != EntityStatusEnum.Active && service.Status == EntityStatusEnum.Active)
        {
            var subsCount = await CountActiveSubscriptionsConsumingService(serviceId);
            if (subsCount > 0)
                return Result.Failure<bool>(ServiceError.HasActiveSubscriptions(serviceId, subsCount));
        }

        service.Status = status;

        _context.Services.Update(service);

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    private async Task<int> CountActiveSubscriptionsConsumingService(int serviceId)
    {
        return await _context.FrequentSubscriptions.CountAsync(s =>
            s.Status == EntityStatusEnum.Active &&
            (s.OutboundServiceId == serviceId ||
             (s.InboundServiceId == serviceId && s.ReserveTypeId == ReserveTypeIdEnum.IdaVuelta)));
    }

    //TODO: Contemplar ServiceDirections en ReserveDirections.
    public async Task<Result<bool>> GenerateFutureReservesAsync()
    {
        await MarkOldReservesAsExpiredAsync();

        var today = dateTimeProvider.LocalNow;
        var endDate = today.AddDays(await GetReserveGenerationDaysAsync());

        var services = await _context.Services
            .Where(s => s.Status == EntityStatusEnum.Active)
            .Include(s => s.Trip)
            .Include(s => s.Trip.OriginCity)
            .Include(s => s.Trip.DestinationCity)
            .ToListAsync();

        var activeServices = services.Where(s => s.Trip.Status == EntityStatusEnum.Active).ToList();

        if (activeServices.Count == 0)
        {
            await _context.SaveChangesWithOutboxAsync();
            return true;
        }

        var tripIds = activeServices.Select(s => s.TripId).Distinct().ToList();
        var windowStart = today.Date;
        var windowEnd = endDate.Date.AddDays(1);

        var existingSlots = await _context.Reserves
            .Where(r => tripIds.Contains(r.TripId)
                     && r.ReserveDate >= windowStart
                     && r.ReserveDate < windowEnd
                     && r.Status != ReserveStatusEnum.Cancelled
                     && r.Status != ReserveStatusEnum.Expired)
            .Select(r => new { r.TripId, r.ReserveDate, r.DepartureHour })
            .ToListAsync();

        var existingSlotSet = existingSlots
            .Select(s => (s.TripId, s.ReserveDate.Date, s.DepartureHour))
            .ToHashSet();

        foreach (var service in activeServices)
        {
            for (var date = today; date <= endDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek != service.DayOfWeek)
                    continue;

                if (IsHoliday(date) && !service.IsHoliday)
                    continue;

                var slotKey = (service.TripId, date.Date, service.DepartureHour);
                if (existingSlotSet.Contains(slotKey))
                    continue;

                var fullReserveDate = date.Date + service.DepartureHour;

                var reserve = new Reserve
                {
                    ReserveDate = fullReserveDate,
                    ServiceId = service.ServiceId,
                    VehicleId = service.VehicleId,
                    Status = ReserveStatusEnum.Confirmed,
                    DepartureHour = service.DepartureHour,
                    EstimatedDuration = service.EstimatedDuration,
                    IsHoliday = service.IsHoliday,
                    ServiceName = service.Name,
                    TripId = service.TripId,
                    OriginName = service.Trip.OriginCity.Name,
                    DestinationName = service.Trip.DestinationCity.Name,
                };

                _context.Reserves.Add(reserve);
                existingSlotSet.Add(slotKey);
            }
        }

        await _context.SaveChangesWithOutboxAsync();
        return true;
    }

    private async Task MarkOldReservesAsExpiredAsync()
    {
        var now = dateTimeProvider.LocalNow;

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

    public async Task<Result<List<ServiceListItemDto>>> GetActiveServicesListAsync()
    {
        var services = await _context.Services
            .AsNoTracking()
            .Where(s => s.Status == EntityStatusEnum.Active)
            .Select(s => new ServiceListItemDto(
                s.ServiceId,
                s.Name,
                s.TripId,
                s.Trip.Description,
                s.Trip.OriginCityId,
                s.Trip.DestinationCityId,
                s.DayOfWeek,
                s.DepartureHour,
                s.AllowedDirections.Select(ad => ad.DirectionId).ToList()))
            .ToListAsync();

        return Result.Success(services);
    }

    // Lee el ReserveGenerationDays del TenantConfig del tenant actual. Si no existe TenantConfig
    // (tenant recién creado, deuda histórica), cae al default global de IReserveOption.
    // NOTA: TenantConfig NO implementa ITenantScoped — hay que filtrar explícito por TenantId.
    private async Task<int> GetReserveGenerationDaysAsync()
    {
        var configValue = await _context.TenantConfigs
            .Where(tc => tc.TenantId == _tenantContext.TenantId)
            .Select(tc => (int?)tc.ReserveGenerationDays)
            .FirstOrDefaultAsync();
        return configValue ?? _reserveOption.ReserveGenerationDays;
    }
}
