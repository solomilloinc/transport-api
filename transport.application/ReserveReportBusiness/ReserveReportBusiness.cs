using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Transport.Business.Data;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Reserves.Abstraction;
using Transport.Domain.Trips;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveReportBusiness;

public class ReserveReportBusiness : IReserveReportBusiness
{
    private readonly IApplicationDbContext _context;

    public ReserveReportBusiness(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>> GetReserveReport(
        DateTime reserveDate, PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto)
    {
        var date = reserveDate.Date;

        var baseQuery = _context.Reserves
            .Include(r => r.Passengers)
            .Include(r => r.Vehicle)
            .Where(r => r.Status == ReserveStatusEnum.Confirmed)
            .Where(r => r.ReserveDate.Date == date);

        var totalCount = await baseQuery.CountAsync();

        var sortBy = requestDto.SortBy?.ToLower() ?? "reservedate";
        var sortDesc = requestDto.SortDescending;

        IOrderedQueryable<Reserve> orderedQuery = sortBy switch
        {
            "serviceorigin" => sortDesc ? baseQuery.OrderByDescending(r => r.OriginName) : baseQuery.OrderBy(r => r.OriginName),
            "servicedest" => sortDesc ? baseQuery.OrderByDescending(r => r.DestinationName) : baseQuery.OrderBy(r => r.DestinationName),
            _ => sortDesc ? baseQuery.OrderByDescending(r => r.ReserveDate) : baseQuery.OrderBy(r => r.ReserveDate)
        };

        var pageNumber = requestDto.PageNumber > 0 ? requestDto.PageNumber : 1;
        var pageSize = requestDto.PageSize > 0 ? requestDto.PageSize : 10;

        var reserves = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var tripIds = reserves.Select(r => r.TripId).Distinct().ToList();

        var tripDescriptions = await _context.Trips
            .Where(t => tripIds.Contains(t.TripId))
            .ToDictionaryAsync(t => t.TripId, t => t.Description);

        var items = reserves.Select(r =>
        {
            var tripName = tripDescriptions.GetValueOrDefault(r.TripId) ?? "Unknown Trip";

            return new ReserveReportResponseDto(
                r.ReserveId,
                r.TripId,
                tripName,
                r.OriginName,
                r.DestinationName,
                r.Vehicle.AvailableQuantity,
                r.Passengers.Count,
                r.DepartureHour.ToString(@"hh\:mm"),
                r.VehicleId,
                r.DriverId.GetValueOrDefault(),
                r.ReserveDate
            );
        }).ToList();

        var pagedResult = new PagedReportResponseDto<ReserveReportResponseDto>
        {
            Items = items,
            TotalRecords = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return Result.Success(pagedResult);
    }

    public async Task<Result<ReserveGroupedPagedReportResponseDto>> GetReserveReport(
        PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto)
    {
        var idaDate = requestDto.Filters.DepartureDate.Date;
        var vueltaDate = requestDto.Filters.ReturnDate?.Date;
        var passengersRequested = requestDto.Filters.Passengers;
        var tripId = requestDto.Filters.TripId;

        var idaTrip = await _context.Trips
            .Where(t => t.TripId == tripId && t.Status == EntityStatusEnum.Active)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
            .FirstOrDefaultAsync();

        if (idaTrip == null)
            return Result.Failure<ReserveGroupedPagedReportResponseDto>(TripError.TripNotFound);

        var originId = idaTrip.OriginCityId;
        var destinationId = idaTrip.DestinationCityId;

        var pickupDirectionId = requestDto.Filters.PickupDirectionId;
        List<TripPickupStop>? idaTripPickupStops = null;
        if (pickupDirectionId.HasValue)
        {
            idaTripPickupStops = await _context.TripPickupStops
                .Where(td => td.TripId == tripId && td.Status == EntityStatusEnum.Active)
                .Include(td => td.Direction)
                .OrderBy(td => td.Order)
                .ToListAsync();

            if (!idaTripPickupStops.Any(td => td.DirectionId == pickupDirectionId.Value))
                return Result.Failure<ReserveGroupedPagedReportResponseDto>(TripError.TripPickupStopNotFound);
        }

        Trip? vueltaTrip = null;
        List<TripPickupStop>? vueltaTripPickupStops = null;
        if (vueltaDate.HasValue)
        {
            vueltaTrip = await _context.Trips
                .Where(t => t.OriginCityId == destinationId && t.DestinationCityId == originId
                         && t.Status == EntityStatusEnum.Active)
                .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
                .FirstOrDefaultAsync();

            if (vueltaTrip != null && pickupDirectionId.HasValue)
            {
                vueltaTripPickupStops = await _context.TripPickupStops
                    .Where(td => td.TripId == vueltaTrip.TripId && td.Status == EntityStatusEnum.Active)
                    .Include(td => td.Direction)
                    .OrderBy(td => td.Order)
                    .ToListAsync();
            }
        }

        var idaPrice = idaTrip.Prices
            .Where(p => p.ReserveTypeId == ReserveTypeIdEnum.Ida && p.CityId == destinationId && p.DirectionId == null)
            .Select(p => p.Price)
            .FirstOrDefault();

        var vueltaPrice = vueltaTrip?.Prices
            .Where(p => p.ReserveTypeId == ReserveTypeIdEnum.Ida && p.CityId == originId && p.DirectionId == null)
            .Select(p => p.Price)
            .FirstOrDefault() ?? 0;

        var query = _context.Reserves
            .Include(r => r.Vehicle)
            .Include(r => r.Passengers)
            .Include(r => r.Trip)
            .Where(rp => rp.Status == ReserveStatusEnum.Confirmed &&
                        (rp.ReserveDate.Date == idaDate ||
                         (vueltaDate.HasValue && rp.ReserveDate.Date == vueltaDate.Value)));

        var items = await query.ToListAsync();

        var idaItems = items
            .Where(rp => rp.ReserveDate.Date == idaDate)
            .Where(rp => rp.TripId == tripId)
            .Where(rp =>
            {
                int totalReserved = rp.Passengers
                    .Count(p => p.Status == PassengerStatusEnum.Confirmed
                             || p.Status == PassengerStatusEnum.PendingPayment);
                var available = rp.Vehicle.AvailableQuantity - totalReserved;
                return available >= passengersRequested;
            })
            .Select(rp =>
            {
                int totalReserved = rp.Passengers
                    .Count(p => p.Status == PassengerStatusEnum.Confirmed
                             || p.Status == PassengerStatusEnum.PendingPayment);
                var arrivalTime = rp.DepartureHour.Add(rp.EstimatedDuration);
                var stopSchedules = idaTripPickupStops?.Select(td => new ReserveStopScheduleDto(
                    td.DirectionId,
                    td.Direction.Name,
                    td.Order,
                    rp.DepartureHour.Add(td.PickupTimeOffset).ToString(@"hh\:mm")
                )).ToList();
                return new ReserveExternalReportResponseDto(
                    rp.ReserveId,
                    rp.OriginName,
                    rp.DestinationName,
                    rp.DepartureHour.ToString(@"hh\:mm"),
                    rp.ReserveDate,
                    rp.EstimatedDuration.ToString(@"hh\:mm"),
                    arrivalTime.ToString(@"hh\:mm"),
                    idaPrice,
                    rp.Vehicle.AvailableQuantity - totalReserved,
                    rp.Vehicle.InternalNumber,
                    rp.TripId,
                    stopSchedules
                );
            })
            .ToList();

        var vueltaItems = vueltaDate.HasValue && vueltaTrip != null
            ? items
                .Where(rp => rp.ReserveDate.Date == vueltaDate.Value)
                .Where(rp => rp.TripId == vueltaTrip.TripId)
                .Where(rp =>
                {
                    int totalReserved = rp.Passengers
                        .Count(p => p.Status == PassengerStatusEnum.Confirmed
                                 || p.Status == PassengerStatusEnum.PendingPayment);
                    var available = rp.Vehicle.AvailableQuantity - totalReserved;
                    return available >= passengersRequested;
                })
                .Select(rp =>
                {
                    int totalReserved = rp.Passengers
                        .Count(p => p.Status == PassengerStatusEnum.Confirmed
                                 || p.Status == PassengerStatusEnum.PendingPayment);
                    var arrivalTime = rp.DepartureHour.Add(rp.EstimatedDuration);
                    var vueltaStopSchedules = vueltaTripPickupStops?.Select(td => new ReserveStopScheduleDto(
                        td.DirectionId,
                        td.Direction.Name,
                        td.Order,
                        rp.DepartureHour.Add(td.PickupTimeOffset).ToString(@"hh\:mm")
                    )).ToList();
                    return new ReserveExternalReportResponseDto(
                        rp.ReserveId,
                        rp.OriginName,
                        rp.DestinationName,
                        rp.DepartureHour.ToString(@"hh\:mm"),
                        rp.ReserveDate,
                        rp.EstimatedDuration.ToString(@"hh\:mm"),
                        arrivalTime.ToString(@"hh\:mm"),
                        vueltaPrice,
                        rp.Vehicle.AvailableQuantity - totalReserved,
                        rp.Vehicle.InternalNumber,
                        rp.TripId,
                        vueltaStopSchedules
                    );
                })
                .ToList()
            : new List<ReserveExternalReportResponseDto>();

        var pagedOutbound = PagedReportResponseDto<ReserveExternalReportResponseDto>.Create(
            idaItems,
            requestDto.PageNumber,
            requestDto.PageSize
        );

        var pagedReturn = PagedReportResponseDto<ReserveExternalReportResponseDto>.Create(
            vueltaItems,
            requestDto.PageNumber,
            requestDto.PageSize
        );

        var result = new ReserveGroupedPagedReportResponseDto
        {
            Outbound = pagedOutbound,
            Return = pagedReturn
        };

        return Result.Success(result);
    }

    public async Task<Result<PagedReportResponseDto<PassengerReserveReportResponseDto>>> GetReservePassengerReport(
        int reserveId,
        PagedReportRequestDto<PassengerReserveReportFilterRequestDto> requestDto)
    {
        var query = _context.Passengers
            .Include(p => p.Customer)
            .Include(p => p.Reserve)
                .ThenInclude(r => r.Vehicle)
            .Where(p => p.ReserveId == reserveId);

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.PassengerFullName))
        {
            var nameFilter = requestDto.Filters.PassengerFullName.ToLower();
            query = query.Where(p =>
                (p.FirstName + " " + p.LastName).ToLower().Contains(nameFilter));
        }

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.DocumentNumber))
        {
            var docFilter = requestDto.Filters.DocumentNumber.Trim();
            query = query.Where(p => p.DocumentNumber.Contains(docFilter));
        }

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.Email))
        {
            var emailFilter = requestDto.Filters.Email.ToLower().Trim();
            query = query.Where(p => p.Email != null && p.Email.ToLower().Contains(emailFilter));
        }

        var passengers = await query.ToListAsync();

        var totalPassengersInReserve = await _context.Passengers
            .CountAsync(p => p.ReserveId == reserveId);

        var relatedReserveIds = passengers
            .Where(p => p.ReserveRelatedId.HasValue)
            .Select(p => p.ReserveRelatedId!.Value)
            .Distinct()
            .ToList();

        var allRelevantReserveIds = new List<int> { reserveId };
        allRelevantReserveIds.AddRange(relatedReserveIds);

        var reservePayments = await _context.ReservePayments
            .AsNoTracking()
            .Where(rp => allRelevantReserveIds.Contains(rp.ReserveId))
            .ToListAsync();

        var paymentsByCustomer = new Dictionary<int, (string Methods, decimal Amount)>();

        var localParentPayments = reservePayments
            .Where(p => p.ParentReservePaymentId == null && p.CustomerId.HasValue)
            .GroupBy(p => p.CustomerId!.Value);

        foreach (var group in localParentPayments)
        {
            var parentPayments = group.ToList();
            var breakdownChildren = reservePayments
                .Where(p => p.ParentReservePaymentId != null
                    && p.Amount > 0
                    && parentPayments.Any(pp => pp.ReservePaymentId == p.ParentReservePaymentId))
                .ToList();

            var paymentsForMethods = breakdownChildren.Any() ? breakdownChildren : parentPayments;
            var methods = paymentsForMethods
                .Select(p => GetPaymentMethodName(p.Method))
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            var totalAmount = parentPayments.Sum(p => p.Amount);

            paymentsByCustomer[group.Key] = (string.Join(", ", methods), totalAmount);
        }

        var sortMappings = new Dictionary<string, Expression<Func<Passenger, object>>>
        {
            ["passengerfullname"] = p => p.FirstName + " " + p.LastName,
            ["documentnumber"] = p => p.DocumentNumber,
            ["email"] = p => p.Email
        };

        if (!string.IsNullOrWhiteSpace(requestDto.SortBy) && sortMappings.ContainsKey(requestDto.SortBy.ToLower()))
        {
            var sortKey = sortMappings[requestDto.SortBy.ToLower()].Compile();
            passengers = requestDto.SortDescending
                ? passengers.OrderByDescending(sortKey).ToList()
                : passengers.OrderBy(sortKey).ToList();
        }

        var firstPassenger = passengers.FirstOrDefault();
        var routePrices = new Dictionary<ReserveTypeIdEnum, decimal>();
        if (firstPassenger != null)
        {
            var trip = await _context.Trips
                .Where(t => t.TripId == firstPassenger.Reserve.TripId)
                .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
                .FirstOrDefaultAsync();

            if (trip != null)
            {
                foreach (var price in trip.Prices.Where(p => p.DirectionId == null))
                {
                    routePrices[price.ReserveTypeId] = price.Price;
                }
            }
        }

        var allItems = passengers.Select(p =>
        {
            var paymentInfo = p.CustomerId.HasValue && paymentsByCustomer.ContainsKey(p.CustomerId.Value)
                ? paymentsByCustomer[p.CustomerId.Value]
                : (Methods: (string?)null, Amount: 0m);

            var paidAmount = paymentInfo.Amount;
            var isPayment = paymentInfo.Amount > 0;

            if (!isPayment)
            {
                var typeId = p.ReserveRelatedId.HasValue ? ReserveTypeIdEnum.IdaVuelta : ReserveTypeIdEnum.Ida;
                paidAmount = routePrices.TryGetValue(typeId, out var price) ? price : 0;
            }

            return new PassengerReserveReportResponseDto(
                p.PassengerId,
                p.CustomerId,
                $"{p.FirstName} {p.LastName}",
                p.DocumentNumber,
                p.Email,
                p.Phone,
                p.ReserveId,
                p.DropoffLocationId ?? 0,
                p.DropoffAddress,
                p.PickupLocationId ?? 0,
                p.PickupAddress,
                p.Customer?.CurrentBalance ?? 0,
                p.Reserve.Vehicle.AvailableQuantity - totalPassengersInReserve,
                paymentInfo.Methods,
                paidAmount,
                isPayment,
                p.HasTraveled,
                (int)p.Status);
        }).ToList();

        var pagedResult = PagedReportResponseDto<PassengerReserveReportResponseDto>.Create(
            allItems,
            requestDto.PageNumber,
            requestDto.PageSize);

        return Result.Success(pagedResult);
    }

    public async Task<Result<PagedReportResponseDto<ReservePaymentSummaryResponseDto>>> GetReservePaymentSummary(
        int reserveId,
        PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto> requestDto)
    {
        var reserve = await _context.Reserves
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReserveId == reserveId);

        if (reserve is null)
            return Result.Failure<PagedReportResponseDto<ReservePaymentSummaryResponseDto>>(ReserveError.NotFound);

        var payments = await _context.ReservePayments
            .AsNoTracking()
            .Where(p => p.ReserveId == reserveId)
            .ToListAsync();

        var parentPayments = payments.Where(p => p.ParentReservePaymentId == null).ToList();
        var childBreakdownPayments = payments
            .Where(p => p.ParentReservePaymentId != null && p.Amount > 0)
            .ToList();

        var paymentsForSummary = childBreakdownPayments.Any() ? childBreakdownPayments : parentPayments;

        var paymentsByMethod = paymentsForSummary
            .GroupBy(p => p.Method)
            .Select(g => new PaymentMethodSummaryDto(
                (int)g.Key,
                GetPaymentMethodName(g.Key),
                g.Sum(p => p.Amount)))
            .OrderBy(p => p.PaymentMethodId)
            .ToList();

        var totalAmount = parentPayments.Sum(p => p.Amount);

        var summaryItem = new ReservePaymentSummaryResponseDto(
            reserveId,
            paymentsByMethod,
            totalAmount);

        var result = PagedReportResponseDto<ReservePaymentSummaryResponseDto>.Create(
            new List<ReservePaymentSummaryResponseDto> { summaryItem },
            requestDto.PageNumber,
            requestDto.PageSize);

        return Result.Success(result);
    }

    private static string GetPaymentMethodName(PaymentMethodEnum method)
    {
        return method switch
        {
            PaymentMethodEnum.Cash => "Efectivo",
            PaymentMethodEnum.Online => "Online",
            PaymentMethodEnum.CreditCard => "Tarjeta de Crédito",
            PaymentMethodEnum.Transfer => "Transferencia",
            _ => method.ToString()
        };
    }
}
