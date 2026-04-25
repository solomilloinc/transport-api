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

        var service = new Service
        {
            Name = requestDto.Name,
            TripId = requestDto.TripId,
            EstimatedDuration = requestDto.EstimatedDuration,
            VehicleId = requestDto.VehicleId,
            // StartDay/EndDay vienen del DTO (obligatorios — ver ServiceCreateRequestDto).
            // Antes de exponerlos, ambos quedaban con el default de DayOfWeek (Sunday=0)
            // y los servicios solo generaban reservas los domingos.
            StartDay = requestDto.StartDay,
            EndDay = requestDto.EndDay,
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

        // Add allowed directions whitelist
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
            ["status"] = s => s.Status
        };

        var pagedResult = await query.ToPagedReportAsync<ServiceReportResponseDto, Service, ServiceReportFilterRequestDto>(
            requestDto,
            selector: s => new ServiceReportResponseDto(
                s.ServiceId,
                s.Name,
                s.TripId,
                s.Trip.OriginCityId,
                s.Trip.OriginCity.Name,
                s.Trip.DestinationCityId,
                s.Trip.DestinationCity.Name,
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
                )).ToList(),
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

    /// <summary>
    /// Actualiza los datos editables de un Service. Los campos que se pueden cambiar
    /// están definidos en <see cref="ServiceUpdateRequestDto"/>.
    /// </summary>
    /// <remarks>
    /// Cambios respecto a la versión anterior:
    /// <list type="bullet">
    ///   <item>Recibe <see cref="ServiceUpdateRequestDto"/> (antes aceptaba
    ///     <c>ServiceCreateRequestDto</c>, lo que era un desalineo con la OpenAPI).</item>
    ///   <item>Se agregan <see cref="Service.StartDay"/> y <see cref="Service.EndDay"/>
    ///     al set de campos actualizables — antes no había forma de corregirlos
    ///     desde la API y quedaban permanentemente en el default (0,0 = solo Domingo).</item>
    ///   <item><c>TripId</c> es editable. Se valida que el Trip destino exista y esté
    ///     Active (mismo criterio que en <see cref="Create"/>). Las reservas ya
    ///     generadas conservan su <c>TripId</c> propio — solo las reservas futuras que
    ///     genere el batch tomarán el TripId nuevo. Ver XML doc del DTO para los casos
    ///     de uso (corrección de configuración, ajuste de ruta, migración de Trip).</item>
    ///   <item>Se quitó el soft-delete + recreación de <c>Schedules</c>: los schedules
    ///     tienen endpoints propios (<c>service-schedule-create</c>,
    ///     <c>service-schedule-update</c>, <c>service-schedule-delete</c>,
    ///     <c>service-schedule-status</c>). Gestionarlos acá invalidaba reservas ya
    ///     ligadas a un <c>ServiceScheduleId</c> cada vez que se editaba el servicio.</item>
    /// </list>
    /// </remarks>
    public async Task<Result<bool>> Update(int serviceId, ServiceUpdateRequestDto dto)
    {
        var service = await _context.Services
            .Include(s => s.AllowedDirections)
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId);

        if (service == null)
            return Result.Failure<bool>(ServiceError.ServiceNotFound);

        // Validar el Trip solo si cambió — evita un roundtrip innecesario a DB
        // en el caso común de editar solo Name/EstimatedDuration/días de operación.
        if (service.TripId != dto.TripId)
        {
            var trip = await _context.Trips
                .Where(x => x.TripId == dto.TripId)
                .FirstOrDefaultAsync();

            if (trip is null)
                return Result.Failure<bool>(TripError.TripNotFound);

            if (trip.Status != EntityStatusEnum.Active)
                return Result.Failure<bool>(TripError.TripNotActive);

            service.TripId = dto.TripId;
        }

        service.Name = dto.Name;
        service.EstimatedDuration = dto.EstimatedDuration;
        service.VehicleId = dto.VehicleId;
        // StartDay/EndDay: ver Service.StartDay para la semántica del rango + wraparound.
        service.StartDay = dto.StartDay;
        service.EndDay = dto.EndDay;

        // Update allowed directions whitelist (replace all)
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
            .Include(s => s.Trip.OriginCity)
            .Include(s => s.Trip.DestinationCity)
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
                        OriginName = service.Trip.OriginCity.Name,
                        DestinationName = service.Trip.DestinationCity.Name,
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
        var service = await _context.Services.Where(x => x.ServiceId == serviceId).FirstOrDefaultAsync();
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
        var schedule = await _context.ServiceSchedules.Where(x => x.ServiceScheduleId == scheduleId).FirstOrDefaultAsync();

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
        var schedule = await _context.ServiceSchedules.Where(x => x.ServiceScheduleId == scheduleId).FirstOrDefaultAsync();

        if (schedule is null)
            return Result.Failure<bool>(ServiceError.ServiceScheduleNotFound);

        schedule.Status = EntityStatusEnum.Deleted;

        _context.ServiceSchedules.Update(schedule);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    /// <summary>
    /// Sincroniza de forma declarativa la lista completa de schedules de un servicio.
    /// Implementa un "diff" contra DB y aplica Create / Update / soft-Delete en una
    /// única llamada a <c>SaveChangesWithOutboxAsync</c>, que EF Core envuelve en
    /// transacción implícita — o pasan todas las operaciones o ninguna.
    /// </summary>
    /// <remarks>
    /// Clasificación de cada item del payload:
    /// <list type="bullet">
    ///   <item><c>ServiceScheduleId == null</c> → se crea un schedule nuevo.</item>
    ///   <item><c>ServiceScheduleId</c> presente y existente en este servicio →
    ///     se actualizan <c>DepartureHour</c> / <c>IsHoliday</c>. Si además el schedule
    ///     estaba <c>Deleted</c>, se reactiva (<c>Status = Active</c>) — permite "undo"
    ///     desde el frontend mientras el ID siga siendo conocido.</item>
    ///   <item><c>ServiceScheduleId</c> presente en DB pero ausente del payload →
    ///     se hace soft-delete (<c>Status = Deleted</c>). No se hace hard-delete para
    ///     preservar la integridad de reservas históricas que lo referencien.</item>
    /// </list>
    ///
    /// Se rechaza con error toda la operación si el payload incluye un scheduleId
    /// que no pertenece al servicio indicado — evita que un bug/manipulación del
    /// frontend afecte schedules de otros servicios.
    /// </remarks>
    public async Task<Result<bool>> SyncSchedules(int serviceId, ServiceSchedulesSyncRequestDto request)
    {
        var service = await _context.Services
            .Include(s => s.Schedules)
            .FirstOrDefaultAsync(s => s.ServiceId == serviceId);

        if (service is null)
            return Result.Failure<bool>(ServiceError.ServiceNotFound);

        // Schedules actualmente persistidos (incluye Deleted — permite reactivación
        // si el frontend manda un Id que estaba soft-deleteado).
        var existingById = service.Schedules.ToDictionary(s => s.ServiceScheduleId);

        // Validación previa: todos los IDs del payload deben pertenecer al servicio.
        // Se hace ANTES de mutar nada para garantizar el "todo o nada" sin depender
        // del rollback transaccional.
        foreach (var item in request.Schedules.Where(i => i.ServiceScheduleId.HasValue))
        {
            if (!existingById.ContainsKey(item.ServiceScheduleId!.Value))
                return Result.Failure<bool>(
                    ServiceError.ScheduleNotInService(item.ServiceScheduleId.Value, serviceId));
        }

        var payloadIds = request.Schedules
            .Where(i => i.ServiceScheduleId.HasValue)
            .Select(i => i.ServiceScheduleId!.Value)
            .ToHashSet();

        // 1) Soft-delete: schedules activos en DB que no aparecen en el payload.
        foreach (var existing in service.Schedules)
        {
            if (existing.Status == EntityStatusEnum.Deleted)
                continue; // ya estaba borrado — si el payload no lo trae, no hay cambio

            if (!payloadIds.Contains(existing.ServiceScheduleId))
            {
                existing.Status = EntityStatusEnum.Deleted;
                _context.ServiceSchedules.Update(existing);
            }
        }

        // 2) Updates y creates.
        foreach (var item in request.Schedules)
        {
            if (item.ServiceScheduleId.HasValue)
            {
                var existing = existingById[item.ServiceScheduleId.Value];
                existing.DepartureHour = item.DepartureHour;
                existing.IsHoliday = item.IsHoliday;
                // Reactivación si estaba borrado — ver <remarks> del método.
                if (existing.Status == EntityStatusEnum.Deleted)
                    existing.Status = EntityStatusEnum.Active;

                _context.ServiceSchedules.Update(existing);
            }
            else
            {
                _context.ServiceSchedules.Add(new ServiceSchedule
                {
                    ServiceId = serviceId,
                    DepartureHour = item.DepartureHour,
                    IsHoliday = item.IsHoliday,
                    Status = EntityStatusEnum.Active
                });
            }
        }

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateScheduleStatus(int scheduleId, EntityStatusEnum status)
    {
        var schedule = await _context.ServiceSchedules.Where(x => x.ServiceScheduleId == scheduleId).FirstOrDefaultAsync();

        if (schedule is null)
            return Result.Failure<bool>(ServiceError.ServiceScheduleNotFound);

        schedule.Status = status;

        _context.ServiceSchedules.Update(schedule);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<List<ServiceIdNameDto>>> GetActiveServicesListAsync()
    {
        var services = await _context.Services
            .AsNoTracking()
            .Where(s => s.Status == EntityStatusEnum.Active)
            .Select(s => new ServiceIdNameDto(s.ServiceId, s.Name))
            .ToListAsync();

        return Result.Success(services);
    }
}
