using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.CashBoxes;
using Transport.Domain.CashBoxes.Abstraction;
using Transport.Domain.Customers;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.CashBox;
using Transport.SharedKernel.Contracts.Reserve;
using Transport.Domain.Reserves;

namespace Transport.Business.CashBoxBusiness;

public class CashBoxBusiness : ICashBoxBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContext _userContext;

    public CashBoxBusiness(IApplicationDbContext context, IUserContext userContext)
    {
        _context = context;
        _userContext = userContext;
    }

    public async Task<Result<CashBoxResponseDto>> CloseCashBox(CloseCashBoxRequestDto request)
    {
        var cashBox = await _context.CashBoxes
            .Include(c => c.OpenedByUser)
            .FirstOrDefaultAsync(c => c.Status == CashBoxStatusEnum.Open);

        if (cashBox is null)
            return Result.Failure<CashBoxResponseDto>(CashBoxError.NoOpenCashBox);

        // TODO: Validar pagos pendientes cuando se confirme la funcionalidad
        // var hasPendingPayments = await _context.ReservePayments
        //     .AnyAsync(p => p.CashBoxId == cashBox.CashBoxId && p.Status == StatusPaymentEnum.Pending);
        // if (hasPendingPayments)
        //     return Result.Failure<CashBoxResponseDto>(CashBoxError.CannotCloseWithPendingPayments);

        // Cerrar la caja actual
        cashBox.Description = request.Description;
        cashBox.Status = CashBoxStatusEnum.Closed;
        cashBox.ClosedAt = DateTime.UtcNow; //VER
        cashBox.ClosedByUserId = _userContext.UserId;

        _context.CashBoxes.Update(cashBox);

        // Abrir una nueva caja automáticamente
        var newCashBox = new CashBox
        {
            OpenedAt = DateTime.UtcNow,
            Status = CashBoxStatusEnum.Open,
            OpenedByUserId = _userContext.UserId
        };

        _context.CashBoxes.Add(newCashBox);

        await _context.SaveChangesWithOutboxAsync();

        return await GetCashBoxResponse(newCashBox.CashBoxId);
    }

    public async Task<Result<CashBoxResponseDto>> GetCurrentCashBox()
    {
        var cashBox = await _context.CashBoxes
            .FirstOrDefaultAsync(c => c.Status == CashBoxStatusEnum.Open);

        if (cashBox is null)
            return Result.Failure<CashBoxResponseDto>(CashBoxError.NoOpenCashBox);

        return await GetCashBoxResponse(cashBox.CashBoxId);
    }

    public async Task<Result<CashBox>> GetOpenCashBoxEntity()
    {
        var cashBox = await _context.CashBoxes
            .FirstOrDefaultAsync(c => c.Status == CashBoxStatusEnum.Open);

        if (cashBox is null)
            return Result.Failure<CashBox>(CashBoxError.NoOpenCashBox);

        return Result.Success(cashBox);
    }

    public async Task<Result<PagedReportResponseDto<CashBoxResponseDto>>> GetCashBoxReport(
        PagedReportRequestDto<CashBoxReportFilterRequestDto> requestDto)
    {
        var query = _context.CashBoxes
            .Include(c => c.OpenedByUser)
            .Include(c => c.ClosedByUser)
            .AsQueryable();

        if (requestDto.Filters?.FromDate is not null)
            query = query.Where(c => c.OpenedAt >= requestDto.Filters.FromDate);

        if (requestDto.Filters?.ToDate is not null)
            query = query.Where(c => c.OpenedAt <= requestDto.Filters.ToDate);

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.Status))
        {
            if (Enum.TryParse<CashBoxStatusEnum>(requestDto.Filters.Status, true, out var status))
                query = query.Where(c => c.Status == status);
        }

        var sortMappings = new Dictionary<string, Expression<Func<CashBox, object>>>
        {
            ["cashboxid"] = c => c.CashBoxId,
            ["openedat"] = c => c.OpenedAt,
            ["status"] = c => c.Status
        };

        var cashBoxIds = await query
            .OrderByDescending(c => c.OpenedAt)
            .Skip((requestDto.PageNumber - 1) * requestDto.PageSize)
            .Take(requestDto.PageSize)
            .Select(c => c.CashBoxId)
            .ToListAsync();

        var totalCount = await query.CountAsync();

        var responses = new List<CashBoxResponseDto>();
        foreach (var id in cashBoxIds)
        {
            var response = await GetCashBoxResponse(id);
            if (response.IsSuccess)
                responses.Add(response.Value);
        }

        return Result.Success(new PagedReportResponseDto<CashBoxResponseDto>
        {
            Items = responses,
            TotalRecords = totalCount,
            PageNumber = requestDto.PageNumber,
            PageSize = requestDto.PageSize
        });
    }

    private async Task<Result<CashBoxResponseDto>> GetCashBoxResponse(int cashBoxId)
    {
        var cashBox = await _context.CashBoxes
            .Include(c => c.OpenedByUser)
            .Include(c => c.ClosedByUser)
            .FirstOrDefaultAsync(c => c.CashBoxId == cashBoxId);

        if (cashBox is null)
            return Result.Failure<CashBoxResponseDto>(CashBoxError.NotFound);

        // Obtener todos los pagos asociados a esta caja
        var allPayments = await _context.ReservePayments
            .Where(p => p.CashBoxId == cashBoxId)
            .ToListAsync();

        // Separar pagos padres de hijos (Breakdown: ParentId != null && Amount > 0)
        var parentPayments = allPayments.Where(p => p.ParentReservePaymentId == null).ToList();
        var childBreakdownPayments = allPayments
            .Where(p => p.ParentReservePaymentId != null && p.Amount > 0)
            .ToList();

        // Seleccionar los pagos para el resumen: si un padre tiene hijos de desglose, usamos los hijos.
        // Si no tiene, usamos el padre. Esto evita duplicados y asegura el detalle correcto.
        var paymentsForSummary = new List<ReservePayment>();
        foreach (var parent in parentPayments)
        {
            var children = childBreakdownPayments.Where(c => c.ParentReservePaymentId == parent.ReservePaymentId).ToList();
            if (children.Any())
                paymentsForSummary.AddRange(children);
            else
                paymentsForSummary.Add(parent);
        }

        var paymentsByMethod = paymentsForSummary
            .GroupBy(p => p.Method)
            .Select(g => new PaymentMethodSummaryDto(
                (int)g.Key,
                GetPaymentMethodName(g.Key),
                g.Sum(p => p.Amount)))
            .OrderBy(p => p.PaymentMethodId)
            .ToList();

        // Total es la suma de los pagos padres (no los hijos para evitar duplicar)
        var totalAmount = parentPayments.Sum(p => p.Amount);
        var totalPaymentsCount = parentPayments.Count;

        return Result.Success(new CashBoxResponseDto(
            cashBox.CashBoxId,
            cashBox.Description,
            cashBox.OpenedAt,
            cashBox.ClosedAt,
            cashBox.Status.ToString(),
            cashBox.OpenedByUser.Email,
            cashBox.ClosedByUser?.Email,
            cashBox.ReserveId,
            totalPaymentsCount,
            totalAmount,
            paymentsByMethod
        ));
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
