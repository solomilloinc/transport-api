using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.Directions;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Tenants.Abstraction;
using Transport.Domain.Trips;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Internal;

/// <summary>
/// Resuelve, en una sola pasada antes del loop principal, todos los datos
/// que necesita la construcción de Passenger: Reserve (con relaciones), precio
/// aplicado (con regla de combo mismo día), direcciones, mapa ida/vuelta y
/// chequeo de capacidad. El loop posterior solo construye entidades.
/// </summary>
internal sealed class ReservePassengerItemsEnricher
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantReserveConfigProvider _tenantReserveConfigProvider;

    public ReservePassengerItemsEnricher(
        IApplicationDbContext context,
        ITenantReserveConfigProvider tenantReserveConfigProvider)
    {
        _context = context;
        _tenantReserveConfigProvider = tenantReserveConfigProvider;
    }

    public async Task<Result<List<EnrichedPassengerItem>>> EnrichForAdminAsync(
        List<PassengerReserveCreateRequestDto> items)
    {
        var distinctReserveIds = items.Select(i => i.ReserveId).Distinct().ToList();
        var relatedMap = BuildReserveRelatedMap(distinctReserveIds);
        var reserveDates = await LoadReserveDatesAsync(distinctReserveIds);

        var mainReserveId = items.Min(i => i.ReserveId);
        var enriched = new List<EnrichedPassengerItem>(items.Count);

        foreach (var dto in items)
        {
            var reserveResult = await LoadReserveForAdminAsync(dto.ReserveId);
            if (reserveResult.IsFailure)
                return Result.Failure<List<EnrichedPassengerItem>>(reserveResult.Error);

            var reserve = reserveResult.Value;

            var capacityResult = ValidateVehicleCapacity(reserve, items.Count);
            if (capacityResult.IsFailure)
                return Result.Failure<List<EnrichedPassengerItem>>(capacityResult.Error);

            var relatedDate = ResolveRelatedReserveDate(reserve.ReserveId, relatedMap, reserveDates);
            var (price, appliedType) = await GetPassengerPriceAsync(
                reserve.Trip.OriginCityId,
                reserve.Trip.DestinationCityId,
                (ReserveTypeIdEnum)dto.ReserveTypeId,
                dto.DropoffLocationId,
                reserve.ReserveDate,
                relatedDate);

            if (price is null)
                return Result.Failure<List<EnrichedPassengerItem>>(ReserveError.PriceNotAvailable);

            var pickupDirection = await GetDirectionAsync(dto.PickupLocationId, "Pickup");
            if (pickupDirection.IsFailure)
                return Result.Failure<List<EnrichedPassengerItem>>(pickupDirection.Error);

            var dropoffDirection = await GetDirectionAsync(dto.DropoffLocationId, "Dropoff");
            if (dropoffDirection.IsFailure)
                return Result.Failure<List<EnrichedPassengerItem>>(dropoffDirection.Error);

            var inferredRelatedId = relatedMap.TryGetValue(dto.ReserveId, out var rid)
                ? rid
                : dto.ReserveRelatedId;

            enriched.Add(new EnrichedPassengerItem
            {
                AdminDto = dto,
                Reserve = reserve,
                ResolvedPrice = price.Value,
                AppliedReserveType = appliedType,
                IsComboReturnLeg = appliedType == ReserveTypeIdEnum.IdaVuelta && reserve.ReserveId != mainReserveId,
                PickupDirection = pickupDirection.Value,
                DropoffDirection = dropoffDirection.Value,
                InferredReserveRelatedId = inferredRelatedId,
            });
        }

        return Result.Success(enriched);
    }

    public async Task<Result<List<EnrichedPassengerItem>>> EnrichForExternalAsync(
        List<PassengerReserveExternalCreateRequestDto> items)
    {
        var distinctReserveIds = items.Select(i => i.ReserveId).Distinct().OrderBy(id => id).ToList();
        var relatedMap = BuildReserveRelatedMap(distinctReserveIds);
        var reserveDates = await LoadReserveDatesAsync(distinctReserveIds);

        // El externo elige la principal por menor ReserveId (ordenado al armar la lista).
        var mainReserveId = distinctReserveIds.First();
        var enriched = new List<EnrichedPassengerItem>(items.Count);

        foreach (var dto in items)
        {
            var reserveResult = await LoadReserveForExternalAsync(dto.ReserveId);
            if (reserveResult.IsFailure)
                return Result.Failure<List<EnrichedPassengerItem>>(reserveResult.Error);

            var reserve = reserveResult.Value;

            var capacityResult = ValidateVehicleCapacity(reserve, items.Count);
            if (capacityResult.IsFailure)
                return Result.Failure<List<EnrichedPassengerItem>>(capacityResult.Error);

            var relatedDate = ResolveRelatedReserveDate(reserve.ReserveId, relatedMap, reserveDates);
            var (price, appliedType) = await GetPassengerPriceAsync(
                reserve.Trip.OriginCityId,
                reserve.Trip.DestinationCityId,
                (ReserveTypeIdEnum)dto.ReserveTypeId,
                dto.DropoffLocationId,
                reserve.ReserveDate,
                relatedDate);

            if (price is null)
                return Result.Failure<List<EnrichedPassengerItem>>(ReserveError.PriceNotAvailable);

            // Externo: el mismo documento no puede repetirse en la misma reserva.
            if (reserve.Passengers.Any(p => p.DocumentNumber == dto.DocumentNumber))
                return Result.Failure<List<EnrichedPassengerItem>>(
                    ReserveError.PassengerAlreadyExists(dto.DocumentNumber));

            var pickupDirection = await GetDirectionAsync(dto.PickupLocationId, "Pickup");
            if (pickupDirection.IsFailure)
                return Result.Failure<List<EnrichedPassengerItem>>(pickupDirection.Error);

            var dropoffDirection = await GetDirectionAsync(dto.DropoffLocationId, "Dropoff");
            if (dropoffDirection.IsFailure)
                return Result.Failure<List<EnrichedPassengerItem>>(dropoffDirection.Error);

            var existingCustomer = await _context.Customers
                .SingleOrDefaultAsync(c => c.DocumentNumber == dto.DocumentNumber);

            var inferredRelatedId = relatedMap.TryGetValue(dto.ReserveId, out var rid)
                ? rid
                : dto.ReserveRelatedId;

            enriched.Add(new EnrichedPassengerItem
            {
                ExternalDto = dto,
                Reserve = reserve,
                ResolvedPrice = price.Value,
                AppliedReserveType = appliedType,
                IsComboReturnLeg = appliedType == ReserveTypeIdEnum.IdaVuelta && reserve.ReserveId != mainReserveId,
                PickupDirection = pickupDirection.Value,
                DropoffDirection = dropoffDirection.Value,
                InferredReserveRelatedId = inferredRelatedId,
                ExistingCustomer = existingCustomer,
            });
        }

        return Result.Success(enriched);
    }

    /// <summary>
    /// Cuando hay exactamente 2 reservas distintas, las marca como pareja ida/vuelta
    /// (cada una apunta a la otra). En cualquier otro caso devuelve un mapa vacío.
    /// </summary>
    private static Dictionary<int, int?> BuildReserveRelatedMap(List<int> distinctReserveIds)
    {
        var map = new Dictionary<int, int?>();
        if (distinctReserveIds.Count == 2)
        {
            map[distinctReserveIds[0]] = distinctReserveIds[1];
            map[distinctReserveIds[1]] = distinctReserveIds[0];
        }
        return map;
    }

    /// <summary>
    /// Pre-carga las fechas de las reservas referenciadas para que la regla
    /// "combo mismo día" no dependa del orden de iteración.
    /// </summary>
    private async Task<Dictionary<int, DateTime>> LoadReserveDatesAsync(List<int> reserveIds)
    {
        return await _context.Reserves
            .Where(r => reserveIds.Contains(r.ReserveId))
            .Select(r => new { r.ReserveId, r.ReserveDate })
            .ToDictionaryAsync(x => x.ReserveId, x => x.ReserveDate);
    }

    private static DateTime? ResolveRelatedReserveDate(
        int reserveId,
        Dictionary<int, int?> relatedMap,
        Dictionary<int, DateTime> reserveDates)
    {
        if (relatedMap.TryGetValue(reserveId, out var related)
            && related.HasValue
            && reserveDates.TryGetValue(related.Value, out var date))
        {
            return date;
        }
        return null;
    }

    private async Task<Result<Reserve>> LoadReserveForAdminAsync(int reserveId)
    {
        var reserve = await _context.Reserves
            .Include(r => r.Passengers)
            .Include(r => r.Driver)
            .Include(r => r.Trip)
            .Include(r => r.Service!).ThenInclude(s => s.Trip).ThenInclude(t => t.OriginCity)
            .Include(r => r.Service!).ThenInclude(s => s.Trip).ThenInclude(t => t.DestinationCity)
            .SingleOrDefaultAsync(r => r.ReserveId == reserveId);

        if (reserve is null)
            return Result.Failure<Reserve>(ReserveError.NotFound);

        if (reserve.Status != ReserveStatusEnum.Confirmed)
            return Result.Failure<Reserve>(ReserveError.NotAvailable);

        return Result.Success(reserve);
    }

    private async Task<Result<Reserve>> LoadReserveForExternalAsync(int reserveId)
    {
        var reserve = await _context.Reserves
            .Include(r => r.Passengers)
            .Include(r => r.Trip)
            .SingleOrDefaultAsync(r => r.ReserveId == reserveId);

        if (reserve is null)
            return Result.Failure<Reserve>(ReserveError.NotFound);

        if (reserve.Status != ReserveStatusEnum.Confirmed)
            return Result.Failure<Reserve>(ReserveError.NotAvailable);

        return Result.Success(reserve);
    }

    /// <summary>
    /// Preserva la lógica original (item por item, comparando la suma con la
    /// capacidad usando el conteo TOTAL de items). El comportamiento es el mismo
    /// que estaba antes del refactor; cambios en este criterio quedan fuera de scope.
    /// </summary>
    private Result ValidateVehicleCapacity(Reserve reserve, int totalIncomingItems)
    {
        var vehicle = _context.Vehicles.FirstOrDefault(v => v.VehicleId == reserve.VehicleId);
        var existing = reserve.Passengers.Count;
        var afterInsert = existing + totalIncomingItems;

        if (vehicle == null || afterInsert > vehicle.AvailableQuantity)
            return Result.Failure(ReserveError.VehicleQuantityNotAvailable(
                existing, totalIncomingItems, vehicle?.AvailableQuantity ?? 0));

        return Result.Success();
    }

    private async Task<Result<Direction?>> GetDirectionAsync(int? locationId, string type)
    {
        if (locationId is null || locationId == 0)
            return Result.Success<Direction?>(null);

        var direction = await _context.Directions.FirstOrDefaultAsync(x => x.DirectionId == locationId);
        if (direction is null)
        {
            return Result.Failure<Direction?>(Error.NotFound(
                $"Direction.{type}NotFound",
                $"{type} direction not found"));
        }

        return Result.Success<Direction?>(direction);
    }

    private async Task<ReserveTypeIdEnum> ResolveAppliedReserveTypeAsync(
        ReserveTypeIdEnum requestedType,
        DateTime currentReserveDate,
        DateTime? relatedReserveDate)
    {
        if (requestedType != ReserveTypeIdEnum.IdaVuelta)
            return requestedType;

        var config = await _tenantReserveConfigProvider.GetCurrentAsync();
        if (!config.RoundTripSameDayOnly)
            return ReserveTypeIdEnum.IdaVuelta;

        if (!relatedReserveDate.HasValue)
            return ReserveTypeIdEnum.Ida;

        return relatedReserveDate.Value.Date == currentReserveDate.Date
            ? ReserveTypeIdEnum.IdaVuelta
            : ReserveTypeIdEnum.Ida;
    }

    /// <summary>
    /// Replica la búsqueda de precio que vivía en ReserveBusiness.GetPassengerPriceAsync.
    /// Se mantiene como método interno acá para no crear acoplamiento adicional;
    /// la versión pública sigue viva en ReserveBusiness para uso de QuoteAsync.
    /// </summary>
    private async Task<(decimal? Price, ReserveTypeIdEnum AppliedType)> GetPassengerPriceAsync(
        int originId,
        int destinationId,
        ReserveTypeIdEnum reserveTypeId,
        int? dropoffLocationId,
        DateTime currentReserveDate,
        DateTime? relatedReserveDate)
    {
        var appliedType = await ResolveAppliedReserveTypeAsync(reserveTypeId, currentReserveDate, relatedReserveDate);

        var trip = await _context.Trips
            .Where(t => t.OriginCityId == originId
                     && t.DestinationCityId == destinationId
                     && t.Status == EntityStatusEnum.Active)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
            .FirstOrDefaultAsync();

        if (trip is null)
            return (null, appliedType);

        var relevantPrices = trip.Prices.Where(p => p.ReserveTypeId == appliedType).ToList();
        if (!relevantPrices.Any())
            return (null, appliedType);

        int? dropoffCityId = null;
        if (dropoffLocationId.HasValue)
        {
            var dropoffDirection = await _context.Directions
                .FirstOrDefaultAsync(x => x.DirectionId == dropoffLocationId.Value);
            dropoffCityId = dropoffDirection?.CityId;

            var directionPrice = relevantPrices.FirstOrDefault(p => p.DirectionId == dropoffLocationId.Value);
            if (directionPrice != null)
                return (directionPrice.Price, appliedType);
        }

        if (dropoffCityId.HasValue)
        {
            var cityPrice = relevantPrices.FirstOrDefault(p => p.CityId == dropoffCityId.Value && p.DirectionId == null);
            if (cityPrice != null)
                return (cityPrice.Price, appliedType);
        }

        var basePrice = relevantPrices.FirstOrDefault(p => p.CityId == destinationId && p.DirectionId == null);
        return (basePrice?.Price, appliedType);
    }
}
