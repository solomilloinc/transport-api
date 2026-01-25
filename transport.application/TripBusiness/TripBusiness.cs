using System.Linq;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Cities;
using Transport.Domain.Directions;
using Transport.Domain.Reserves;
using Transport.Domain.Trips;
using Transport.Domain.Trips.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Trip;
using Transport.SharedKernel.Contracts.City;
using Transport.SharedKernel.Contracts.Direction;

namespace Transport.Business.TripBusiness;

public class TripBusiness : ITripBusiness
{
    private readonly IApplicationDbContext _context;

    public TripBusiness(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> CreateTrip(TripCreateDto dto)
    {
        if (dto.OriginCityId == dto.DestinationCityId)
            return Result.Failure<int>(TripError.InvalidTripConfiguration);

        var originCity = await _context.Cities.FindAsync(dto.OriginCityId);
        if (originCity is null)
            return Result.Failure<int>(CityError.CityNotFound);

        var destinationCity = await _context.Cities.FindAsync(dto.DestinationCityId);
        if (destinationCity is null)
            return Result.Failure<int>(CityError.CityNotFound);

        var existingTrip = await _context.Trips
            .AnyAsync(t => t.OriginCityId == dto.OriginCityId
                        && t.DestinationCityId == dto.DestinationCityId
                        && t.Status == EntityStatusEnum.Active);

        if (existingTrip)
            return Result.Failure<int>(TripError.TripAlreadyExists);

        var trip = new Trip
        {
            Description = dto.Description,
            OriginCityId = dto.OriginCityId,
            DestinationCityId = dto.DestinationCityId,
            Status = EntityStatusEnum.Active
        };

        _context.Trips.Add(trip);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(trip.TripId);
    }

    public async Task<Result<bool>> UpdateTrip(int tripId, TripCreateDto dto)
    {
        if (dto.OriginCityId == dto.DestinationCityId)
            return Result.Failure<bool>(TripError.InvalidTripConfiguration);

        var trip = await _context.Trips.FindAsync(tripId);
        if (trip is null)
            return Result.Failure<bool>(TripError.TripNotFound);

        var originCity = await _context.Cities.FindAsync(dto.OriginCityId);
        if (originCity is null)
            return Result.Failure<bool>(CityError.CityNotFound);

        var destinationCity = await _context.Cities.FindAsync(dto.DestinationCityId);
        if (destinationCity is null)
            return Result.Failure<bool>(CityError.CityNotFound);

        // Check for duplicate (excluding current trip)
        var existingTrip = await _context.Trips
            .AnyAsync(t => t.OriginCityId == dto.OriginCityId
                        && t.DestinationCityId == dto.DestinationCityId
                        && t.Status == EntityStatusEnum.Active
                        && t.TripId != tripId);

        if (existingTrip)
            return Result.Failure<bool>(TripError.TripAlreadyExists);

        trip.Description = dto.Description;
        trip.OriginCityId = dto.OriginCityId;
        trip.DestinationCityId = dto.DestinationCityId;

        _context.Trips.Update(trip);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> DeleteTrip(int tripId)
    {
        var trip = await _context.Trips.FindAsync(tripId);
        if (trip is null)
            return Result.Failure<bool>(TripError.TripNotFound);

        trip.Status = EntityStatusEnum.Deleted;

        _context.Trips.Update(trip);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateTripStatus(int tripId, EntityStatusEnum status)
    {
        var trip = await _context.Trips.FindAsync(tripId);
        if (trip is null)
            return Result.Failure<bool>(TripError.TripNotFound);

        trip.Status = status;

        _context.Trips.Update(trip);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<PagedReportResponseDto<TripReportResponseDto>>> GetTripReport(
        PagedReportRequestDto<TripReportFilterDto> request)
    {
        var query = _context.Trips
            .AsNoTracking()
            .Include(t => t.OriginCity)
            .Include(t => t.DestinationCity)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
                .ThenInclude(p => p.City)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
                .ThenInclude(p => p.Direction)
            .AsQueryable();

        if (request.Filters?.OriginCityId is not null)
            query = query.Where(t => t.OriginCityId == request.Filters.OriginCityId);

        if (request.Filters?.DestinationCityId is not null)
            query = query.Where(t => t.DestinationCityId == request.Filters.DestinationCityId);

        if (request.Filters?.Status is not null)
            query = query.Where(t => t.Status == request.Filters.Status);

        var sortMappings = new Dictionary<string, Expression<Func<Trip, object>>>
        {
            ["description"] = t => t.Description,
            ["origincityid"] = t => t.OriginCityId,
            ["destinationcityid"] = t => t.DestinationCityId,
            ["status"] = t => t.Status
        };

        var pagedResult = await query.ToPagedReportAsync<TripReportResponseDto, Trip, TripReportFilterDto>(
            request,
            selector: t => new TripReportResponseDto(
                t.TripId,
                t.Description,
                t.OriginCityId,
                t.OriginCity.Name,
                t.DestinationCityId,
                t.DestinationCity.Name,
                t.Status.ToString(),
                t.Prices.OrderBy(p => p.Order).Select(p => new TripPriceReportDto(
                    p.TripPriceId,
                    p.CityId,
                    p.City.Name,
                    p.DirectionId,
                    p.Direction != null ? p.Direction.Name : null,
                    (p.ReserveTypeId == ReserveTypeIdEnum.Ida ? 1 : 2),
                    p.ReserveTypeId.ToString(),
                    p.Price,
                    p.Order,
                    p.Status.ToString(),
                    p.DirectionId.HasValue
                        ? $"{p.Direction!.Name} ({p.City.Name})"
                        : p.City.Name,
                    p.CityId == t.DestinationCityId && p.DirectionId == null
                )).ToList(),
                new List<CityDirectionsDto>(),
                new List<PickupOptionDto>(),
                new List<DropoffOptionDto>(),
                new List<DropoffOptionDto>()
            ),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<TripReportResponseDto>> GetTripById(int tripId, int? reserveId = null)
    {
        var trip = await _context.Trips
            .AsNoTracking()
            .Include(t => t.OriginCity)
            .Include(t => t.DestinationCity)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
                .ThenInclude(p => p.City)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
                .ThenInclude(p => p.Direction)
            .FirstOrDefaultAsync(t => t.TripId == tripId);

        if (trip is null)
            return Result.Failure<TripReportResponseDto>(TripError.TripNotFound);

        // Fetch Directions for Relevant Cities (Origin, Destination, and any Intermediate Price City)
        var relevantCityIds = new HashSet<int> { trip.OriginCityId, trip.DestinationCityId };
        foreach (var price in trip.Prices)
        {
            relevantCityIds.Add(price.CityId);
        }

        var citiesWithDirections = await _context.Cities
            .AsNoTracking()
            .Where(c => relevantCityIds.Contains(c.CityId))
            .Include(c => c.Directions.Where(d => d.Status == EntityStatusEnum.Active))
            .ToListAsync();

        // Get allowed directions whitelist if reserveId is provided
        HashSet<int>? allowedDirectionIds = null;
        if (reserveId.HasValue)
        {
            var reserve = await _context.Reserves
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReserveId == reserveId.Value);

            if (reserve is not null)
            {
                if (reserve.ServiceId.HasValue)
                {
                    // Reserve from batch: get whitelist from ServiceDirections
                    var serviceDirections = await _context.ServiceDirections
                        .AsNoTracking()
                        .Where(sd => sd.ServiceId == reserve.ServiceId.Value)
                        .Select(sd => sd.DirectionId)
                        .ToListAsync();

                    if (serviceDirections.Any())
                        allowedDirectionIds = serviceDirections.ToHashSet();
                }
                else
                {
                    // Individual reserve: get whitelist from ReserveDirections
                    var reserveDirections = await _context.ReserveDirections
                        .AsNoTracking()
                        .Where(rd => rd.ReserveId == reserveId.Value)
                        .Select(rd => rd.DirectionId)
                        .ToListAsync();

                    if (reserveDirections.Any())
                        allowedDirectionIds = reserveDirections.ToHashSet();
                }
            }
        }

        var relevantCitiesDto = citiesWithDirections.Select(c => new CityDirectionsDto(
            c.CityId,
            c.Name,
            c.Directions.Select(d => new DirectionDto(d.DirectionId, d.Name)).ToList()
        )).ToList();

        // Frontend-ready: Pickup options from origin city directions
        var originDirections = citiesWithDirections
            .FirstOrDefault(c => c.CityId == trip.OriginCityId)?
            .Directions ?? new List<Direction>();

        // Filter by whitelist if exists
        if (allowedDirectionIds is not null)
            originDirections = originDirections.Where(d => allowedDirectionIds.Contains(d.DirectionId)).ToList();

        var pickupOptions = originDirections
            .Select(d => new PickupOptionDto(d.DirectionId, d.Name))
            .ToList();

        // Helper to build dropoff options grouped by city
        List<DropoffOptionDto> BuildDropoffOptions(ReserveTypeIdEnum reserveType, bool onlyDestination = false)
        {
            // Get city-level prices (without specific direction)
            var cityPrices = trip.Prices
                .Where(p => p.ReserveTypeId == reserveType && p.DirectionId == null)
                .Where(p => !onlyDestination || p.CityId == trip.DestinationCityId)
                .OrderBy(p => p.Order)
                .ToList();

            return cityPrices.Select(price =>
            {
                // Get ALL directions for this city
                var cityDirections = citiesWithDirections
                    .FirstOrDefault(c => c.CityId == price.CityId)?
                    .Directions ?? new List<Direction>();

                // Filter by whitelist if exists
                if (allowedDirectionIds is not null)
                    cityDirections = cityDirections.Where(d => allowedDirectionIds.Contains(d.DirectionId)).ToList();

                return new DropoffOptionDto(
                    price.CityId,
                    price.City.Name,
                    price.Price,
                    price.CityId == trip.DestinationCityId,
                    cityDirections.Select(d => new DropoffDirectionDto(d.DirectionId, d.Name)).ToList()
                );
            }).ToList();
        }

        // Frontend-ready: Dropoff options separated by reserve type
        var dropoffOptionsIda = BuildDropoffOptions(ReserveTypeIdEnum.Ida);

        // IdaVuelta: SOLO puede bajarse en DestinationCityId
        var dropoffOptionsIdaVuelta = BuildDropoffOptions(ReserveTypeIdEnum.IdaVuelta, onlyDestination: true);

        var dto = new TripReportResponseDto(
            trip.TripId,
            trip.Description,
            trip.OriginCityId,
            trip.OriginCity.Name,
            trip.DestinationCityId,
            trip.DestinationCity.Name,
            trip.Status.ToString(),
            trip.Prices.OrderBy(p => p.Order).Select(p => new TripPriceReportDto(
                p.TripPriceId,
                p.CityId,
                p.City.Name,
                p.DirectionId,
                p.Direction?.Name,
                (p.ReserveTypeId == ReserveTypeIdEnum.Ida ? 1 : 2),
                p.ReserveTypeId.ToString(),
                p.Price,
                p.Order,
                p.Status.ToString(),
                DisplayName: p.DirectionId.HasValue
                    ? $"{p.Direction!.Name} ({p.City.Name})"
                    : p.City.Name,
                IsMainDestination: p.CityId == trip.DestinationCityId && p.DirectionId == null
            )).ToList(),
            relevantCitiesDto,
            pickupOptions,
            dropoffOptionsIda,
            dropoffOptionsIdaVuelta
        );

        return Result.Success(dto);
    }

    public async Task<Result<int>> AddPrice(TripPriceCreateDto dto)
    {
        var trip = await _context.Trips.FindAsync(dto.TripId);
        if (trip is null)
            return Result.Failure<int>(TripError.TripNotFound);

        var city = await _context.Cities.FindAsync(dto.CityId);
        if (city is null)
            return Result.Failure<int>(CityError.CityNotFound);

        if (dto.DirectionId.HasValue)
        {
            var direction = await _context.Directions.FindAsync(dto.DirectionId.Value);
            if (direction is null)
                return Result.Failure<int>(CityError.CityNotFound); // TODO: Create DirectionError
        }

        // Check for duplicate price
        var existingPrice = await _context.TripPrices
            .AnyAsync(p => p.TripId == dto.TripId
                        && p.CityId == dto.CityId
                        && p.DirectionId == dto.DirectionId
                        && p.ReserveTypeId == (ReserveTypeIdEnum)dto.ReserveTypeId
                        && p.Status == EntityStatusEnum.Active);

        if (existingPrice)
            return Result.Failure<int>(TripError.TripPriceAlreadyExists);

        var tripPrice = new TripPrice
        {
            TripId = dto.TripId,
            CityId = dto.CityId,
            DirectionId = dto.DirectionId,
            ReserveTypeId = (ReserveTypeIdEnum)dto.ReserveTypeId,
            Price = dto.Price,
            Order = dto.Order,
            Status = EntityStatusEnum.Active
        };

        _context.TripPrices.Add(tripPrice);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(tripPrice.TripPriceId);
    }

    public async Task<Result<bool>> UpdatePrice(int tripPriceId, TripPriceUpdateDto dto)
    {
        var tripPrice = await _context.TripPrices.FindAsync(tripPriceId);
        if (tripPrice is null)
            return Result.Failure<bool>(TripError.TripPriceNotFound);

        var city = await _context.Cities.FindAsync(dto.CityId);
        if (city is null)
            return Result.Failure<bool>(CityError.CityNotFound);

        if (dto.DirectionId.HasValue)
        {
            var direction = await _context.Directions.FindAsync(dto.DirectionId.Value);
            if (direction is null)
                return Result.Failure<bool>(CityError.CityNotFound); // TODO: Create DirectionError
        }

        // Check for duplicate price (excluding current)
        var existingPrice = await _context.TripPrices
            .AnyAsync(p => p.TripId == tripPrice.TripId
                        && p.CityId == dto.CityId
                        && p.DirectionId == dto.DirectionId
                        && p.ReserveTypeId == (ReserveTypeIdEnum)dto.ReserveTypeId
                        && p.Status == EntityStatusEnum.Active
                        && p.TripPriceId != tripPriceId);

        if (existingPrice)
            return Result.Failure<bool>(TripError.TripPriceAlreadyExists);

        tripPrice.CityId = dto.CityId;
        tripPrice.DirectionId = dto.DirectionId;
        tripPrice.ReserveTypeId = (ReserveTypeIdEnum)dto.ReserveTypeId;
        tripPrice.Price = dto.Price;
        tripPrice.Order = dto.Order;

        _context.TripPrices.Update(tripPrice);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> DeletePrice(int tripPriceId)
    {
        var tripPrice = await _context.TripPrices.FindAsync(tripPriceId);
        if (tripPrice is null)
            return Result.Failure<bool>(TripError.TripPriceNotFound);

        tripPrice.Status = EntityStatusEnum.Deleted;

        _context.TripPrices.Update(tripPrice);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdatePricesByPercentage(PriceMassiveUpdateDto dto)
    {
        var activePrices = await _context.TripPrices
            .Where(p => p.Status == EntityStatusEnum.Active)
            .ToListAsync();

        foreach (var priceUpdate in dto.PriceUpdates)
        {
            var matchingPrices = activePrices
                .Where(p => p.ReserveTypeId == (ReserveTypeIdEnum)priceUpdate.ReserveTypeId);

            foreach (var price in matchingPrices)
            {
                var originalPrice = price.Price;
                var increase = originalPrice * (priceUpdate.Percentage / 100m);
                price.Price = decimal.Round(originalPrice + increase, 2);
            }
        }

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    /// <summary>
    /// Gets the price for a reservation based on dropoff location.
    /// Price lookup priority:
    /// 1. Specific direction price (if dropoffDirectionId provided)
    /// 2. City price (if dropoffCityId provided)
    /// 3. Destination city price (base price)
    /// </summary>
    public async Task<Result<decimal>> GetPriceForReservation(
        int tripId,
        int? dropoffCityId,
        int? dropoffDirectionId,
        int reserveTypeId)
    {
        var trip = await _context.Trips
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
            .FirstOrDefaultAsync(t => t.TripId == tripId);

        if (trip is null)
            return Result.Failure<decimal>(TripError.TripNotFound);

        var reserveType = (ReserveTypeIdEnum)reserveTypeId;
        var prices = trip.Prices.Where(p => p.ReserveTypeId == reserveType).ToList();

        if (!prices.Any())
            return Result.Failure<decimal>(TripError.TripPriceNotFound);

        // 1. Try to find price by specific direction
        if (dropoffDirectionId.HasValue)
        {
            var directionPrice = prices.FirstOrDefault(p => p.DirectionId == dropoffDirectionId);
            if (directionPrice is not null)
                return Result.Success(directionPrice.Price);
        }

        // 2. Try to find price by city (without direction)
        if (dropoffCityId.HasValue)
        {
            var cityPrice = prices.FirstOrDefault(p => p.CityId == dropoffCityId && p.DirectionId == null);
            if (cityPrice is not null)
                return Result.Success(cityPrice.Price);
        }

        // 3. Fall back to destination city price (base price)
        var destinationPrice = prices.FirstOrDefault(p => p.CityId == trip.DestinationCityId && p.DirectionId == null);
        if (destinationPrice is not null)
            return Result.Success(destinationPrice.Price);

        return Result.Failure<decimal>(TripError.TripPriceNotFound);
    }

    /// <summary>
    /// Gets active trips for public landing page display.
    /// Returns basic trip info with optional minimum price.
    /// </summary>
    public async Task<Result<PagedReportResponseDto<PublicTripDto>>> GetPublicTrips(int pageNumber = 1, int pageSize = 100)
    {
        var query = _context.Trips
            .AsNoTracking()
            .Where(t => t.Status == EntityStatusEnum.Active)
            .Include(t => t.OriginCity)
            .Include(t => t.DestinationCity)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active));

        var totalRecords = await query.CountAsync();

        var trips = await query
            .OrderBy(t => t.OriginCity.Name)
            .ThenBy(t => t.DestinationCity.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new PublicTripDto(
                t.TripId,
                t.Description,
                t.OriginCityId,
                t.OriginCity.Name,
                t.DestinationCityId,
                t.DestinationCity.Name,
                t.Prices.Any() ? t.Prices.Min(p => p.Price) : null,
                null // EstimatedDuration - could be added if available from Service
            ))
            .ToListAsync();

        var result = new PagedReportResponseDto<PublicTripDto>
        {
            Items = trips,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords
        };

        return Result.Success(result);
    }
}
