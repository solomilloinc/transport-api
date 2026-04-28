using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Internal;

public sealed class ReservePassengerReportReader
{
    private readonly IApplicationDbContext _context;

    public ReservePassengerReportReader(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedReportResponseDto<PassengerReserveReportResponseDto>>> GetReservePassengerReportAsync(
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

        var bookingIdsTouchingLegs = await _context.BookingLegs
            .AsNoTracking()
            .Where(bl => allRelevantReserveIds.Contains(bl.ReserveId))
            .Select(bl => bl.BookingId)
            .Distinct()
            .ToListAsync();

        var reservePayments = await _context.ReservePayments
            .AsNoTracking()
            .Where(rp => allRelevantReserveIds.Contains(rp.ReserveId)
                || (rp.BookingId.HasValue && bookingIdsTouchingLegs.Contains(rp.BookingId.Value)))
            .ToListAsync();

        var attributedPaidByReserveAndCustomer = new Dictionary<(int ReserveId, int CustomerId), decimal>();
        foreach (var (rid, cid) in passengers
                     .Where(p => p.CustomerId.HasValue)
                     .Select(p => (p.ReserveId, p.CustomerId!.Value))
                     .Distinct())
        {
            attributedPaidByReserveAndCustomer[(rid, cid)] =
                await ReservePaidParentPaymentsQueries.SumPaidParentsAttributedToReserveForCustomerAsync(
                    _context, rid, cid);
        }

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

        var allItems = passengers.Select(p =>
        {
            var paymentInfo = p.CustomerId.HasValue && paymentsByCustomer.ContainsKey(p.CustomerId.Value)
                ? paymentsByCustomer[p.CustomerId.Value]
                : (Methods: (string?)null, Amount: 0m);

            decimal paidAmount;
            var isPayment = false;

            if (p.CustomerId.HasValue
                && attributedPaidByReserveAndCustomer.TryGetValue((p.ReserveId, p.CustomerId.Value), out var attributedPaid)
                && attributedPaid > 0)
            {
                paidAmount = attributedPaid;
                isPayment = true;
            }
            else
            {
                paidAmount = paymentInfo.Amount;
                isPayment = paymentInfo.Amount > 0;

                if (!isPayment)
                {
                    // Sin pago real, devolvemos el precio persistido del pasajero.
                    paidAmount = p.Price;
                }
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
