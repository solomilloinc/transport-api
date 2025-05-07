using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Cities;
using Transport.Domain.Drivers;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Services.Abstraction;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ServiceBusiness;

public class ServiceBusiness : IServiceBusiness
{
    private readonly IApplicationDbContext _context;

    public ServiceBusiness(IApplicationDbContext context)
    {
        _context = context;
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

        Service service = new Service
        {
            Name = requestDto.Name,
            StartDay = (DayOfWeek)requestDto.StartDay,
            EndDay = (DayOfWeek)requestDto.StartDay,
            OriginId = requestDto.OriginId,
            DestinationId = requestDto.DestinationId,
            EstimatedDuration = requestDto.EstimatedDuration,
            DepartureHour = requestDto.DepartureHour,
            IsHoliday = requestDto.IsHoliday,
            VehicleId = requestDto.VehicleId,
        };

        foreach (var price in requestDto.Prices)
        {
            service.ReservePrices.Add(new ReservePrice
            {
                Price = price.Price,
                ReserveTypeId = (ReserveTypeIdEnum)price.ReserveTypeId
            });
        }

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
                s.Origin.Name,
                s.Destination.Name,
                s.EstimatedDuration,
                s.DepartureHour,
                s.IsHoliday,
                new ServiceVehicleResponseDto(s.Vehicle.InternalNumber,
                    s.Vehicle.AvailableQuantity,
                    s.Vehicle.VehicleType.Quantity,
                    s.Vehicle.VehicleType.Name,
                    s.Vehicle.VehicleType.ImageBase64),
                s.Status.ToString()
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

        foreach (var price in service.ReservePrices)
        {
            price.Status = EntityStatusEnum.Inactive;
        }

        foreach (var price in dto.Prices)
        {
            service.ReservePrices.Add(new ReservePrice
            {
                Price = price.Price,
                ReserveTypeId = (ReserveTypeIdEnum)price.ReserveTypeId
            });
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

}
