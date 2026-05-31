using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.FrequentSubscriptions;
using Transport.Domain.FrequentSubscriptions.Abstraction;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.FrequentSubscription;

namespace Transport.Business.FrequentSubscriptionBusiness;

public class FrequentSubscriptionBusiness : IFrequentSubscriptionBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IFrequentPassengerBusiness _frequentPassengerBusiness;
    private readonly ILogger<FrequentSubscriptionBusiness>? _logger;

    public FrequentSubscriptionBusiness(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IFrequentPassengerBusiness frequentPassengerBusiness,
        ILogger<FrequentSubscriptionBusiness>? logger = null)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _frequentPassengerBusiness = frequentPassengerBusiness;
        _logger = logger;
    }

    public async Task<Result<int>> Create(FrequentSubscriptionCreateRequestDto dto)
    {
        if (!Enum.IsDefined(typeof(ReserveTypeIdEnum), dto.ReserveTypeId))
            return Result.Failure<int>(FrequentSubscriptionError.InvalidIdaConfig);

        var reserveType = (ReserveTypeIdEnum)dto.ReserveTypeId;

        var shapeError = ValidateReserveTypeShape(reserveType, dto);
        if (shapeError is not null) return Result.Failure<int>(shapeError);

        var startDate = (dto.StartDate ?? _dateTimeProvider.LocalNow).Date;
        var endDate = dto.EndDate?.Date;
        if (endDate.HasValue && endDate.Value < startDate)
            return Result.Failure<int>(FrequentSubscriptionError.InvalidDateRange);

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == dto.CustomerId);
        if (customer is null || customer.Status != EntityStatusEnum.Active)
            return Result.Failure<int>(CustomerError.NotFound);

        var outbound = await LoadServiceForSubscription(dto.OutboundServiceId);
        if (outbound is null)
            return Result.Failure<int>(ServiceError.ServiceNotActive(dto.OutboundServiceId));

        Service? inbound = null;
        if (reserveType == ReserveTypeIdEnum.IdaVuelta)
        {
            inbound = await LoadServiceForSubscription(dto.InboundServiceId!.Value);
            if (inbound is null)
                return Result.Failure<int>(ServiceError.ServiceNotActive(dto.InboundServiceId.Value));
        }

        var directionError = ValidateDirectionsAgainstAllowed(dto, outbound, inbound);
        if (directionError is not null) return Result.Failure<int>(directionError);

        var duplicate = await _context.FrequentSubscriptions.AnyAsync(s =>
            s.CustomerId == dto.CustomerId &&
            s.OutboundServiceId == dto.OutboundServiceId &&
            s.Status == EntityStatusEnum.Active);
        if (duplicate)
            return Result.Failure<int>(
                FrequentSubscriptionError.OverlapWithExistingSubscription(dto.CustomerId, dto.OutboundServiceId));

        var capacityError = await ValidateCapacity(outbound, inbound, reserveType, excludeSubscriptionId: null);
        if (capacityError is not null) return Result.Failure<int>(capacityError);

        var subscription = new FrequentSubscription
        {
            CustomerId = dto.CustomerId,
            ReserveTypeId = reserveType,
            OutboundServiceId = dto.OutboundServiceId,
            InboundServiceId = dto.InboundServiceId,
            OutboundPickupLocationId = dto.OutboundPickupLocationId,
            OutboundDropoffLocationId = dto.OutboundDropoffLocationId,
            InboundPickupLocationId = dto.InboundPickupLocationId,
            InboundDropoffLocationId = dto.InboundDropoffLocationId,
            StartDate = startDate,
            EndDate = endDate,
            Status = EntityStatusEnum.Active
        };

        _context.FrequentSubscriptions.Add(subscription);
        await _context.SaveChangesWithOutboxAsync();

        // Auto-apply: enlazar Passengers a las Reserves ya existentes en la ventana, sin esperar al batch.
        // Best-effort: si falla por alguna razón transitoria, la sub queda persistida y el próximo run del
        // batch (idempotente) la pickea. No queremos romper el Create por un problema de generación.
        try
        {
            var applyResult = await _frequentPassengerBusiness
                .GenerateForSubscriptionAsync(subscription.FrequentSubscriptionId);
            if (applyResult.IsFailure)
                _logger?.LogWarning(
                    "Auto-apply de FrequentSubscription {Id} falló: {Error}. La próxima corrida del batch la pickea.",
                    subscription.FrequentSubscriptionId, applyResult.Error.Code);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Auto-apply de FrequentSubscription {Id} lanzó excepción. La próxima corrida del batch la pickea.",
                subscription.FrequentSubscriptionId);
        }

        return Result.Success(subscription.FrequentSubscriptionId);
    }

    public async Task<Result<bool>> Update(int frequentSubscriptionId, FrequentSubscriptionUpdateRequestDto dto)
    {
        var subscription = await _context.FrequentSubscriptions
            .Include(s => s.OutboundService).ThenInclude(svc => svc.AllowedDirections)
            .Include(s => s.InboundService!).ThenInclude(svc => svc.AllowedDirections)
            .FirstOrDefaultAsync(s => s.FrequentSubscriptionId == frequentSubscriptionId);

        if (subscription is null) return Result.Failure<bool>(FrequentSubscriptionError.NotFound);

        if (subscription.ReserveTypeId == ReserveTypeIdEnum.IdaVuelta)
        {
            if (!dto.InboundPickupLocationId.HasValue || !dto.InboundDropoffLocationId.HasValue)
                return Result.Failure<bool>(FrequentSubscriptionError.InvalidIdaVueltaConfig);
        }
        else if (dto.InboundPickupLocationId.HasValue || dto.InboundDropoffLocationId.HasValue)
        {
            return Result.Failure<bool>(FrequentSubscriptionError.InvalidIdaConfig);
        }

        if (!IsDirectionAllowed(subscription.OutboundService, dto.OutboundPickupLocationId))
            return Result.Failure<bool>(
                FrequentSubscriptionError.DirectionNotAllowedForService(
                    subscription.OutboundServiceId, dto.OutboundPickupLocationId, SubscriptionLeg.Outbound, DirectionKind.Pickup));

        if (!IsDirectionAllowed(subscription.OutboundService, dto.OutboundDropoffLocationId))
            return Result.Failure<bool>(
                FrequentSubscriptionError.DirectionNotAllowedForService(
                    subscription.OutboundServiceId, dto.OutboundDropoffLocationId, SubscriptionLeg.Outbound, DirectionKind.Dropoff));

        if (subscription.ReserveTypeId == ReserveTypeIdEnum.IdaVuelta)
        {
            if (!IsDirectionAllowed(subscription.InboundService!, dto.InboundPickupLocationId!.Value))
                return Result.Failure<bool>(
                    FrequentSubscriptionError.DirectionNotAllowedForService(
                        subscription.InboundServiceId!.Value, dto.InboundPickupLocationId.Value, SubscriptionLeg.Inbound, DirectionKind.Pickup));

            if (!IsDirectionAllowed(subscription.InboundService!, dto.InboundDropoffLocationId!.Value))
                return Result.Failure<bool>(
                    FrequentSubscriptionError.DirectionNotAllowedForService(
                        subscription.InboundServiceId!.Value, dto.InboundDropoffLocationId.Value, SubscriptionLeg.Inbound, DirectionKind.Dropoff));
        }

        var today = _dateTimeProvider.LocalNow.Date;
        if (dto.StartDate.HasValue)
        {
            if (subscription.StartDate <= today && dto.StartDate.Value.Date != subscription.StartDate)
                return Result.Failure<bool>(FrequentSubscriptionError.CannotChangeStartDateAlreadyStarted);
            subscription.StartDate = dto.StartDate.Value.Date;
        }

        var newEndDate = dto.EndDate?.Date;
        if (newEndDate.HasValue && newEndDate.Value < subscription.StartDate)
            return Result.Failure<bool>(FrequentSubscriptionError.InvalidDateRange);

        subscription.EndDate = newEndDate;
        subscription.OutboundPickupLocationId = dto.OutboundPickupLocationId;
        subscription.OutboundDropoffLocationId = dto.OutboundDropoffLocationId;
        subscription.InboundPickupLocationId = dto.InboundPickupLocationId;
        subscription.InboundDropoffLocationId = dto.InboundDropoffLocationId;

        _context.FrequentSubscriptions.Update(subscription);
        await _context.SaveChangesWithOutboxAsync();

        // Auto-apply post-Update (mismo patrón que Create): si el update extendió el rango de
        // fechas, se crean Passengers para las Reserves que ahora caen dentro. Es idempotente —
        // los Passengers existentes mantienen su snapshot (ADR 0001) sin importar lo que se editó.
        // Si la edición fue sólo pickup/dropoff sin cambio de fechas, es un no-op (correcto:
        // los Passengers viejos no se tocan, los nuevos que se generen tomarán el pickup nuevo).
        try
        {
            var applyResult = await _frequentPassengerBusiness
                .GenerateForSubscriptionAsync(subscription.FrequentSubscriptionId);
            if (applyResult.IsFailure)
                _logger?.LogWarning(
                    "Auto-apply post-Update de FrequentSubscription {Id} falló: {Error}. La próxima corrida del batch la pickea.",
                    subscription.FrequentSubscriptionId, applyResult.Error.Code);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Auto-apply post-Update de FrequentSubscription {Id} lanzó excepción. La próxima corrida del batch la pickea.",
                subscription.FrequentSubscriptionId);
        }

        return Result.Success(true);
    }

    public async Task<Result<bool>> Cancel(int frequentSubscriptionId)
    {
        return await _unitOfWork.ExecuteInTransactionAsync<bool>(async () =>
        {
            var subscription = await _context.FrequentSubscriptions
                .FirstOrDefaultAsync(s => s.FrequentSubscriptionId == frequentSubscriptionId);
            if (subscription is null) return Result.Failure<bool>(FrequentSubscriptionError.NotFound);
            if (subscription.Status != EntityStatusEnum.Active)
                return Result.Failure<bool>(FrequentSubscriptionError.AlreadyCancelled);

            subscription.Status = EntityStatusEnum.Deleted;

            var now = _dateTimeProvider.UtcNow;        // instante UTC: para el Date del refund
            var localNow = _dateTimeProvider.LocalNow; // hora local: para comparar ReserveDate (agenda)
            var futurePassengers = await _context.Passengers
                .Where(p => p.FrequentSubscriptionId == frequentSubscriptionId
                         && !p.HasTraveled
                         && p.Status != PassengerStatusEnum.Cancelled
                         && p.Status != PassengerStatusEnum.Traveled)
                .Include(p => p.Reserve)
                .ToListAsync();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == subscription.CustomerId);
            if (customer is null) return Result.Failure<bool>(CustomerError.NotFound);

            foreach (var passenger in futurePassengers)
            {
                if (passenger.Reserve.ReserveDate < localNow) continue;

                passenger.Status = PassengerStatusEnum.Cancelled;

                if (passenger.Price > 0)
                {
                    var refund = new CustomerAccountTransaction
                    {
                        CustomerId = customer.CustomerId,
                        Date = now,
                        Type = TransactionType.Refund,
                        Amount = -passenger.Price,
                        Description = $"Cancelación de pasajero frecuente (suscripción {frequentSubscriptionId}) en reserva {passenger.ReserveId}.",
                        RelatedReserveId = passenger.ReserveId
                    };
                    _context.CustomerAccountTransactions.Add(refund);
                    customer.CurrentBalance -= passenger.Price;
                }
            }

            // Marca explícita: convención del codebase. Sin esto el cambio a CurrentBalance puede perderse.
            if (futurePassengers.Count > 0)
                _context.Customers.Update(customer);

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    public async Task<Result<FrequentSubscriptionCancelPreviewDto>> GetCancelPreview(int frequentSubscriptionId)
    {
        var subscription = await _context.FrequentSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.FrequentSubscriptionId == frequentSubscriptionId);

        if (subscription is null)
            return Result.Failure<FrequentSubscriptionCancelPreviewDto>(FrequentSubscriptionError.NotFound);

        if (subscription.Status != EntityStatusEnum.Active)
            return Result.Failure<FrequentSubscriptionCancelPreviewDto>(FrequentSubscriptionError.AlreadyCancelled);

        var localNow = _dateTimeProvider.LocalNow;

        // Mismo filtro que aplica Cancel: futuros, no-viajados, no-cancelados.
        var preview = await _context.Passengers
            .Where(p => p.FrequentSubscriptionId == frequentSubscriptionId
                     && !p.HasTraveled
                     && p.Status != PassengerStatusEnum.Cancelled
                     && p.Status != PassengerStatusEnum.Traveled
                     && p.Reserve.ReserveDate >= localNow)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Total = g.Sum(p => p.Price)
            })
            .FirstOrDefaultAsync();

        var dto = new FrequentSubscriptionCancelPreviewDto(
            FrequentSubscriptionId: frequentSubscriptionId,
            PassengersToCancel: preview?.Count ?? 0,
            TotalRefundAmount: preview?.Total ?? 0m);

        return Result.Success(dto);
    }

    public async Task<Result<FrequentSubscriptionResponseDto>> GetById(int frequentSubscriptionId)
    {
        var dto = await _context.FrequentSubscriptions
            .AsNoTracking()
            .Where(s => s.FrequentSubscriptionId == frequentSubscriptionId)
            .Select(MapToResponseDto())
            .FirstOrDefaultAsync();

        return dto is null
            ? Result.Failure<FrequentSubscriptionResponseDto>(FrequentSubscriptionError.NotFound)
            : Result.Success(dto);
    }

    public async Task<Result<PagedReportResponseDto<FrequentSubscriptionResponseDto>>> GetReport(
        PagedReportRequestDto<FrequentSubscriptionReportFilterRequestDto> requestDto)
    {
        var query = _context.FrequentSubscriptions.AsNoTracking().AsQueryable();

        var filters = requestDto.Filters;
        if (filters is not null)
        {
            if (filters.CustomerId.HasValue)
                query = query.Where(s => s.CustomerId == filters.CustomerId.Value);
            if (filters.OutboundServiceId.HasValue)
                query = query.Where(s => s.OutboundServiceId == filters.OutboundServiceId.Value);
            if (filters.InboundServiceId.HasValue)
                query = query.Where(s => s.InboundServiceId == filters.InboundServiceId.Value);
            if (filters.ReserveTypeId.HasValue)
            {
                var rt = (ReserveTypeIdEnum)filters.ReserveTypeId.Value;
                query = query.Where(s => s.ReserveTypeId == rt);
            }
            if (filters.Status.HasValue)
                query = query.Where(s => s.Status == filters.Status.Value);
            else
                query = query.Where(s => s.Status == EntityStatusEnum.Active);

            if (filters.ActiveAtDate.HasValue)
            {
                var d = filters.ActiveAtDate.Value.Date;
                query = query.Where(s => s.StartDate <= d && (s.EndDate == null || s.EndDate >= d));
            }
        }
        else
        {
            query = query.Where(s => s.Status == EntityStatusEnum.Active);
        }

        var sortMappings = new Dictionary<string, Expression<Func<FrequentSubscription, object>>>
        {
            ["customerid"] = s => s.CustomerId,
            ["outboundserviceid"] = s => s.OutboundServiceId,
            ["startdate"] = s => s.StartDate,
            ["status"] = s => s.Status
        };

        var paged = await query.ToPagedReportAsync<FrequentSubscriptionResponseDto, FrequentSubscription, FrequentSubscriptionReportFilterRequestDto>(
            requestDto,
            selector: MapToResponseDto(),
            sortMappings: sortMappings
        );

        return Result.Success(paged);
    }

    private static Expression<Func<FrequentSubscription, FrequentSubscriptionResponseDto>> MapToResponseDto() =>
        s => new FrequentSubscriptionResponseDto(
            s.FrequentSubscriptionId,
            s.CustomerId,
            s.Customer.FirstName + " " + s.Customer.LastName,
            s.Customer.DocumentNumber,
            // La columna se persiste como VARCHAR ('Ida'/'IdaVuelta'), así que no se puede CAST a int en SQL.
            // El ternario se traduce a CASE WHEN y resuelve el mapping del lado del motor.
            s.ReserveTypeId == ReserveTypeIdEnum.IdaVuelta ? (int)ReserveTypeIdEnum.IdaVuelta : (int)ReserveTypeIdEnum.Ida,
            s.OutboundServiceId,
            s.OutboundService.Name,
            s.OutboundService.DayOfWeek,
            s.OutboundService.DepartureHour,
            s.InboundServiceId,
            s.InboundService != null ? s.InboundService.Name : null,
            s.InboundService != null ? (DayOfWeek?)s.InboundService.DayOfWeek : null,
            s.InboundService != null ? (TimeSpan?)s.InboundService.DepartureHour : null,
            s.OutboundPickupLocationId,
            s.OutboundPickupLocation.Name,
            s.OutboundDropoffLocationId,
            s.OutboundDropoffLocation.Name,
            s.InboundPickupLocationId,
            s.InboundPickupLocation != null ? s.InboundPickupLocation.Name : null,
            s.InboundDropoffLocationId,
            s.InboundDropoffLocation != null ? s.InboundDropoffLocation.Name : null,
            s.StartDate,
            s.EndDate,
            s.Status.ToString()
        );

    private async Task<Service?> LoadServiceForSubscription(int serviceId)
    {
        var service = await _context.Services
            .Include(s => s.Vehicle)
            .Include(s => s.AllowedDirections)
            .FirstOrDefaultAsync(s => s.ServiceId == serviceId);

        return service is null || service.Status != EntityStatusEnum.Active ? null : service;
    }

    private static Error? ValidateReserveTypeShape(ReserveTypeIdEnum reserveType, FrequentSubscriptionCreateRequestDto dto)
    {
        if (reserveType == ReserveTypeIdEnum.Ida)
        {
            if (dto.InboundServiceId.HasValue || dto.InboundPickupLocationId.HasValue || dto.InboundDropoffLocationId.HasValue)
                return FrequentSubscriptionError.InvalidIdaConfig;
        }
        else if (reserveType == ReserveTypeIdEnum.IdaVuelta)
        {
            if (!dto.InboundServiceId.HasValue || !dto.InboundPickupLocationId.HasValue || !dto.InboundDropoffLocationId.HasValue)
                return FrequentSubscriptionError.InvalidIdaVueltaConfig;
        }
        return null;
    }

    private static Error? ValidateDirectionsAgainstAllowed(
        FrequentSubscriptionCreateRequestDto dto, Service outbound, Service? inbound)
    {
        if (!IsDirectionAllowed(outbound, dto.OutboundPickupLocationId))
            return FrequentSubscriptionError.DirectionNotAllowedForService(
                outbound.ServiceId, dto.OutboundPickupLocationId, SubscriptionLeg.Outbound, DirectionKind.Pickup);
        if (!IsDirectionAllowed(outbound, dto.OutboundDropoffLocationId))
            return FrequentSubscriptionError.DirectionNotAllowedForService(
                outbound.ServiceId, dto.OutboundDropoffLocationId, SubscriptionLeg.Outbound, DirectionKind.Dropoff);
        if (inbound is null) return null;

        if (!IsDirectionAllowed(inbound, dto.InboundPickupLocationId!.Value))
            return FrequentSubscriptionError.DirectionNotAllowedForService(
                inbound.ServiceId, dto.InboundPickupLocationId.Value, SubscriptionLeg.Inbound, DirectionKind.Pickup);
        if (!IsDirectionAllowed(inbound, dto.InboundDropoffLocationId!.Value))
            return FrequentSubscriptionError.DirectionNotAllowedForService(
                inbound.ServiceId, dto.InboundDropoffLocationId.Value, SubscriptionLeg.Inbound, DirectionKind.Dropoff);
        return null;
    }

    private static bool IsDirectionAllowed(Service service, int directionId)
    {
        // Si el Service no tiene whitelist, cualquier direction es válida (consistente con flujo admin).
        if (service.AllowedDirections is null || service.AllowedDirections.Count == 0) return true;
        return service.AllowedDirections.Any(ad => ad.DirectionId == directionId);
    }

    private async Task<Error?> ValidateCapacity(
        Service outbound, Service? inbound, ReserveTypeIdEnum reserveType, int? excludeSubscriptionId)
    {
        var outboundCount = await CountSubscriptionsConsumingService(outbound.ServiceId, excludeSubscriptionId);
        if (outboundCount + 1 > outbound.Vehicle.AvailableQuantity)
            return FrequentSubscriptionError.CapacityExceeded(outbound.ServiceId, outboundCount, outbound.Vehicle.AvailableQuantity);

        if (reserveType == ReserveTypeIdEnum.IdaVuelta && inbound is not null)
        {
            var inboundCount = await CountSubscriptionsConsumingService(inbound.ServiceId, excludeSubscriptionId);
            if (inboundCount + 1 > inbound.Vehicle.AvailableQuantity)
                return FrequentSubscriptionError.CapacityExceeded(inbound.ServiceId, inboundCount, inbound.Vehicle.AvailableQuantity);
        }

        return null;
    }

    private async Task<int> CountSubscriptionsConsumingService(int serviceId, int? excludeSubscriptionId)
    {
        var query = _context.FrequentSubscriptions
            .Where(s => s.Status == EntityStatusEnum.Active &&
                        (s.OutboundServiceId == serviceId ||
                         (s.InboundServiceId == serviceId && s.ReserveTypeId == ReserveTypeIdEnum.IdaVuelta)));

        if (excludeSubscriptionId.HasValue)
            query = query.Where(s => s.FrequentSubscriptionId != excludeSubscriptionId.Value);

        return await query.CountAsync();
    }
}
