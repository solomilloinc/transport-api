using MercadoPago.Resource.Payment;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Transport.Business.Data;
using Transport.Business.Services.Payment;
using Transport.Domain.CashBoxes.Abstraction;
using Transport.Domain.Customers;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Reserves.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Payment;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReservePaymentBusiness;

public class ReservePaymentBusiness : IReservePaymentBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMercadoPagoPaymentGateway _paymentGateway;
    private readonly ICashBoxBusiness _cashBoxBusiness;

    public ReservePaymentBusiness(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IMercadoPagoPaymentGateway paymentGateway,
        ICashBoxBusiness cashBoxBusiness)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _paymentGateway = paymentGateway;
        _cashBoxBusiness = cashBoxBusiness;
    }

    public async Task<Result<bool>> CreatePaymentsAsync(
      int customerId,
      int reserveId,
      List<CreatePaymentRequestDto> payments)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var reserve = await _context.Reserves
                .Include(r => r.Passengers)
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.ReserveId == reserveId);

            if (reserve is null)
                return Result.Failure<bool>(ReserveError.NotFound);

            if (payments == null || !payments.Any())
                return Result.Failure<bool>(Error.Validation("Payments.Empty",
                    "Debe proporcionar al menos un pago."));

            var invalidAmounts = payments
                .Select((p, i) => new { Index = i + 1, Amount = p.TransactionAmount })
                .Where(p => p.Amount <= 0)
                .ToList();

            if (invalidAmounts.Any())
            {
                var errorMsg = string.Join(", ",
                    invalidAmounts.Select(p => $"Pago #{p.Index} tiene monto inválido: {p.Amount}"));
                return Result.Failure<bool>(Error.Validation("Payments.InvalidAmount", errorMsg));
            }

            var duplicatedMethods = payments
                .GroupBy(p => p.PaymentMethod)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatedMethods.Any())
            {
                var duplicatedList = string.Join(", ", duplicatedMethods);
                return Result.Failure<bool>(Error.Validation("Payments.DuplicatedMethod",
                    $"Los métodos de pago no deben repetirse. Duplicados: {duplicatedList}"));
            }

            var totalPassengerPrice = reserve.Passengers.Sum(p => p.Price);
            var providedAmount = payments.Sum(p => p.TransactionAmount);

            var totalAlreadyPaid = await _context.ReservePayments
                .Where(rp => rp.ReserveId == reserveId
                    && rp.ParentReservePaymentId == null
                    && rp.Status == StatusPaymentEnum.Paid)
                .SumAsync(rp => rp.Amount);

            var remainingAmount = totalPassengerPrice - totalAlreadyPaid;

            if (remainingAmount <= 0)
                return Result.Failure<bool>(ReserveError.AlreadyFullyPaid(reserveId));

            if (providedAmount > remainingAmount)
                return Result.Failure<bool>(
                    ReserveError.OverPaymentNotAllowed(remainingAmount, providedAmount));

            Customer payer = await _context.Customers.Where(x => x.CustomerId == customerId).FirstOrDefaultAsync();
            if (payer == null)
                return Result.Failure<bool>(CustomerError.NotFound);

            var cashBoxResult = await _cashBoxBusiness.GetOpenCashBoxEntity();
            if (cashBoxResult.IsFailure)
                return Result.Failure<bool>(cashBoxResult.Error);

            var cashBox = cashBoxResult.Value;
            var primaryMethod = (PaymentMethodEnum)payments.First().PaymentMethod;

            var parentPayment = new ReservePayment
            {
                ReserveId = reserveId,
                CustomerId = payer.CustomerId,
                PayerDocumentNumber = payer.DocumentNumber,
                PayerName = $"{payer.FirstName} {payer.LastName}",
                PayerEmail = payer.Email,
                Amount = providedAmount,
                Method = primaryMethod,
                Status = StatusPaymentEnum.Paid,
                StatusDetail = "paid_on_departure",
                CashBoxId = cashBox.CashBoxId
            };
            _context.ReservePayments.Add(parentPayment);
            await _context.SaveChangesWithOutboxAsync();

            if (payments.Count > 1)
            {
                foreach (var payment in payments)
                {
                    var breakdownChild = new ReservePayment
                    {
                        ReserveId = reserveId,
                        CustomerId = payer.CustomerId,
                        PayerDocumentNumber = payer.DocumentNumber,
                        PayerName = $"{payer.FirstName} {payer.LastName}",
                        PayerEmail = payer.Email,
                        Amount = payment.TransactionAmount,
                        Method = (PaymentMethodEnum)payment.PaymentMethod,
                        Status = StatusPaymentEnum.Paid,
                        ParentReservePaymentId = parentPayment.ReservePaymentId,
                        CashBoxId = cashBox.CashBoxId
                    };
                    _context.ReservePayments.Add(breakdownChild);
                }
            }

            if (totalAlreadyPaid + providedAmount >= totalPassengerPrice)
            {
                foreach (var passenger in reserve.Passengers)
                {
                    if (passenger.Status == PassengerStatusEnum.PendingPayment)
                    {
                        passenger.Status = PassengerStatusEnum.Confirmed;
                        _context.Passengers.Update(passenger);
                    }
                }
            }

            var transaction = new CustomerAccountTransaction
            {
                CustomerId = payer.CustomerId,
                Date = DateTime.UtcNow,
                Type = TransactionType.Payment,
                Amount = -providedAmount,
                Description = $"Pago aplicado a reserva #{reserveId}",
                RelatedReserveId = reserveId,
                ReservePaymentId = parentPayment.ReservePaymentId
            };
            _context.CustomerAccountTransactions.Add(transaction);

            payer.CurrentBalance -= providedAmount;
            _context.Customers.Update(payer);

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    public async Task<Result<bool>> SettleCustomerDebtAsync(SettleCustomerDebtRequestDto request)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var customer = await _context.Customers.Where(x => x.CustomerId == request.CustomerId).FirstOrDefaultAsync();
            if (customer is null)
                return Result.Failure<bool>(CustomerError.NotFound);

            var invalidAmounts = request.Payments
                .Select((p, i) => new { Index = i + 1, Amount = p.TransactionAmount })
                .Where(p => p.Amount <= 0)
                .ToList();

            if (invalidAmounts.Any())
            {
                var errorMsg = string.Join(", ",
                    invalidAmounts.Select(p => $"Pago #{p.Index} tiene monto inválido: {p.Amount}"));
                return Result.Failure<bool>(Error.Validation("Payments.InvalidAmount", errorMsg));
            }

            var duplicatedMethods = request.Payments
                .GroupBy(p => p.PaymentMethod)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatedMethods.Any())
            {
                var duplicatedList = string.Join(", ", duplicatedMethods);
                return Result.Failure<bool>(Error.Validation("Payments.DuplicatedMethod",
                    $"Los métodos de pago no deben repetirse. Duplicados: {duplicatedList}"));
            }

            var reserves = await _context.Reserves
                .Include(r => r.Passengers)
                .Where(r => request.ReserveIds.Contains(r.ReserveId))
                .ToListAsync();

            if (!reserves.Any())
                return Result.Failure<bool>(ReserveError.NotFound);

            var reserveDebts = new List<(Reserve Reserve, decimal Debt)>();
            foreach (var reserve in reserves)
            {
                var customerPassengerPrice = reserve.Passengers
                    .Where(p => p.CustomerId == request.CustomerId)
                    .Sum(p => p.Price);

                var paidAmount = await _context.ReservePayments
                    .Where(rp => rp.ReserveId == reserve.ReserveId
                        && rp.CustomerId == request.CustomerId
                        && rp.ParentReservePaymentId == null
                        && rp.Status == StatusPaymentEnum.Paid)
                    .SumAsync(rp => rp.Amount);

                var debt = customerPassengerPrice - paidAmount;
                if (debt > 0)
                    reserveDebts.Add((reserve, debt));
            }

            if (!reserveDebts.Any())
                return Result.Failure<bool>(ReserveError.NoDebtToSettle);

            var totalPayment = request.Payments.Sum(p => p.TransactionAmount);
            var totalDebt = reserveDebts.Sum(rd => rd.Debt);

            if (totalPayment > totalDebt)
                return Result.Failure<bool>(ReserveError.OverPaymentNotAllowed(totalDebt, totalPayment));

            var cashBoxResult = await _cashBoxBusiness.GetOpenCashBoxEntity();
            if (cashBoxResult.IsFailure)
                return Result.Failure<bool>(cashBoxResult.Error);

            var cashBox = cashBoxResult.Value;
            var mainReserveId = reserveDebts.First().Reserve.ReserveId;
            var primaryMethod = (PaymentMethodEnum)request.Payments.First().PaymentMethod;

            var parentPayment = new ReservePayment
            {
                ReserveId = mainReserveId,
                CustomerId = customer.CustomerId,
                PayerDocumentNumber = customer.DocumentNumber,
                PayerName = $"{customer.FirstName} {customer.LastName}",
                PayerEmail = customer.Email,
                Amount = totalPayment,
                Method = primaryMethod,
                Status = StatusPaymentEnum.Paid,
                StatusDetail = "debt_settlement",
                CashBoxId = cashBox.CashBoxId
            };
            _context.ReservePayments.Add(parentPayment);
            await _context.SaveChangesWithOutboxAsync();

            if (request.Payments.Count > 1)
            {
                foreach (var p in request.Payments)
                {
                    var breakdownChild = new ReservePayment
                    {
                        ReserveId = mainReserveId,
                        CustomerId = customer.CustomerId,
                        PayerDocumentNumber = customer.DocumentNumber,
                        PayerName = $"{customer.FirstName} {customer.LastName}",
                        PayerEmail = customer.Email,
                        Amount = p.TransactionAmount,
                        Method = (PaymentMethodEnum)p.PaymentMethod,
                        Status = StatusPaymentEnum.Paid,
                        ParentReservePaymentId = parentPayment.ReservePaymentId,
                        CashBoxId = cashBox.CashBoxId
                    };
                    _context.ReservePayments.Add(breakdownChild);
                }
            }

            _context.CustomerAccountTransactions.Add(new CustomerAccountTransaction
            {
                CustomerId = customer.CustomerId,
                Date = DateTime.UtcNow,
                Type = TransactionType.Payment,
                Amount = -totalPayment,
                Description = $"Saldo de deuda aplicado a reserva(s) #{string.Join(", #", reserveDebts.Select(rd => rd.Reserve.ReserveId))}",
                RelatedReserveId = mainReserveId,
                ReservePaymentId = parentPayment.ReservePaymentId
            });

            customer.CurrentBalance -= totalPayment;
            _context.Customers.Update(customer);

            var remainingPayment = totalPayment;
            foreach (var (reserve, debt) in reserveDebts)
            {
                if (remainingPayment <= 0) break;

                var appliedToReserve = Math.Min(remainingPayment, debt);
                remainingPayment -= appliedToReserve;

                if (appliedToReserve >= debt)
                {
                    foreach (var passenger in reserve.Passengers.Where(p => p.CustomerId == request.CustomerId))
                    {
                        if (passenger.Status == PassengerStatusEnum.PendingPayment)
                        {
                            passenger.Status = PassengerStatusEnum.Confirmed;
                            _context.Passengers.Update(passenger);
                        }
                    }
                }
            }

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    public async Task<Result<bool>> UpdateReservePaymentsByExternalId(string externalPaymentId)
    {
        if (_context.ReservePayments.Any(p => p.PaymentExternalId == long.Parse(externalPaymentId)
            && p.Status != StatusPaymentEnum.Pending))
        {
            return Result.Success(true);
        }

        Payment mpPayment = await _paymentGateway.GetPaymentAsync(externalPaymentId);

        if (mpPayment.Status.Equals("in_process") || mpPayment.Status.Equals("pending"))
        {
            return true;
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var parentPayment = await _context.ReservePayments
                .FirstOrDefaultAsync(rp => rp.ReservePaymentId == int.Parse(mpPayment.ExternalReference));

            if (parentPayment == null)
                return Result.Failure<bool>(Error.NotFound("Payment.NotFound",
                    "No se encontró el pago con el ID externo proporcionado"));

            var internalStatus = GetPaymentStatusFromExternal(mpPayment.Status);
            if (internalStatus == null)
                return Result.Failure<bool>(Error.Validation("Payment.InvalidStatus",
                    "Estado de pago no reconocido"));

            parentPayment.Status = internalStatus.Value;
            parentPayment.StatusDetail = mpPayment.StatusDetail;
            parentPayment.PaymentExternalId = mpPayment.Id;
            parentPayment.ResultApiExternalRawJson = JsonConvert.SerializeObject(mpPayment);
            parentPayment.UpdatedBy = "System";
            parentPayment.UpdatedDate = DateTime.UtcNow;
            _context.ReservePayments.Update(parentPayment);

            var payerInfo = new MercadoPagoPayerInfo(
                mpPayment.Payer?.Identification?.Number,
                mpPayment.Payer?.Email,
                mpPayment.Payer?.FirstName,
                mpPayment.Payer?.LastName,
                mpPayment.Card?.Cardholder?.Name);

            var children = await _context.ReservePayments
                .Where(c => c.ParentReservePaymentId == parentPayment.ReservePaymentId)
                .ToListAsync();

            foreach (var child in children)
            {
                child.Status = parentPayment.Status;
                child.StatusDetail = parentPayment.StatusDetail;
                _context.ReservePayments.Update(child);
            }

            var reserveIdsToTouch = new List<int> { parentPayment.ReserveId };
            reserveIdsToTouch.AddRange(children.Select(ch => ch.ReserveId));

            var reservesToUpdate = await _context.Reserves
                .Include(r => r.Passengers)
                .Where(r => reserveIdsToTouch.Contains(r.ReserveId))
                .ToListAsync();

            var newPassengerStatus = internalStatus.Value == StatusPaymentEnum.Paid
                ? PassengerStatusEnum.Confirmed
                : PassengerStatusEnum.Cancelled;

            foreach (var reserve in reservesToUpdate)
            {
                foreach (var passenger in reserve.Passengers)
                {
                    passenger.Status = newPassengerStatus;
                    _context.Passengers.Update(passenger);
                }
            }

            if (internalStatus.Value == StatusPaymentEnum.Paid)
            {
                // El pagador real lo define MercadoPago; si el pago llegó sin Customer (Wallet, o
                // Card in_process: ADR 0008) se resuelve acá, ANTES del asiento en cuenta corriente.
                if (parentPayment.CustomerId is null)
                    await AttachPayerCustomerAsync(parentPayment, reservesToUpdate, payerInfo);

                await RegisterApprovedOnlinePurchaseAsync(parentPayment, reservesToUpdate);
            }

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    public async Task<Result<bool>> ProcessPaymentFromWebhook(ExternalPaymentResultDto externalPayment)
    {
        if (_context.ReservePayments.Any(p => p.PaymentExternalId == externalPayment.PaymentExternalId
            && p.Status != StatusPaymentEnum.Pending))
        {
            return Result.Success(true);
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var parentPayment = await _context.ReservePayments
                .FirstOrDefaultAsync(rp => rp.ReservePaymentId == int.Parse(externalPayment.ExternalReference));

            if (parentPayment == null)
                return Result.Failure<bool>(Error.NotFound("Payment.NotFound",
                    "No se encontró el pago con el ID externo proporcionado"));

            var internalStatus = GetPaymentStatusFromExternal(externalPayment.Status);
            if (internalStatus == null)
                return Result.Failure<bool>(Error.Validation("Payment.InvalidStatus",
                    "Estado de pago no reconocido"));

            parentPayment.Status = internalStatus.Value;
            parentPayment.StatusDetail = externalPayment.StatusDetail;
            parentPayment.PaymentExternalId = externalPayment.PaymentExternalId;
            parentPayment.ResultApiExternalRawJson = externalPayment.RawJson;
            parentPayment.UpdatedBy = "System";
            parentPayment.UpdatedDate = DateTime.UtcNow;
            _context.ReservePayments.Update(parentPayment);

            var payerInfo = new MercadoPagoPayerInfo(
                externalPayment.PayerDocumentNumber,
                externalPayment.PayerEmail,
                externalPayment.PayerFirstName,
                externalPayment.PayerLastName,
                externalPayment.CardholderName);

            var children = await _context.ReservePayments
                .Where(c => c.ParentReservePaymentId == parentPayment.ReservePaymentId)
                .ToListAsync();

            foreach (var child in children)
            {
                child.Status = parentPayment.Status;
                child.StatusDetail = parentPayment.StatusDetail;
                _context.ReservePayments.Update(child);
            }

            var reserveIdsToTouch = new List<int> { parentPayment.ReserveId };
            reserveIdsToTouch.AddRange(children.Select(ch => ch.ReserveId));

            var reservesToUpdate = await _context.Reserves
                .Include(r => r.Passengers)
                .Where(r => reserveIdsToTouch.Contains(r.ReserveId))
                .ToListAsync();

            var newPassengerStatus = internalStatus.Value == StatusPaymentEnum.Paid
                ? PassengerStatusEnum.Confirmed
                : PassengerStatusEnum.Cancelled;

            foreach (var reserve in reservesToUpdate)
            {
                foreach (var passenger in reserve.Passengers)
                {
                    passenger.Status = newPassengerStatus;
                    _context.Passengers.Update(passenger);
                }
            }

            if (internalStatus.Value == StatusPaymentEnum.Paid)
            {
                // El pagador real lo define MercadoPago; si el pago llegó sin Customer (Wallet, o
                // Card in_process: ADR 0008) se resuelve acá, ANTES del asiento en cuenta corriente.
                if (parentPayment.CustomerId is null)
                    await AttachPayerCustomerAsync(parentPayment, reservesToUpdate, payerInfo);

                await RegisterApprovedOnlinePurchaseAsync(parentPayment, reservesToUpdate);
            }

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    /// <summary>
    /// Resuelve el Customer del pagador desde el Payer de MercadoPago (ADR 0008) y lo vincula al
    /// pago. Cadena: cliente existente por documento → pasajero del booking con ese documento →
    /// tercero materializado desde Payer/cardholder. Si no se puede resolver, el pago queda sin
    /// Customer y la compra no se asienta (ADR 0009).
    /// </summary>
    private async Task AttachPayerCustomerAsync(
        ReservePayment parentPayment, List<Reserve> reserves, MercadoPagoPayerInfo payerInfo)
    {
        var customer = await PayerCustomerResolver.ResolveOrCreateAsync(
            _context, payerInfo, reserves.SelectMany(r => r.Passengers));
        if (customer is null)
            return;

        parentPayment.CustomerId = customer.CustomerId;
        parentPayment.PayerDocumentNumber = payerInfo.DocumentNumber ?? parentPayment.PayerDocumentNumber;
        parentPayment.PayerEmail = payerInfo.Email ?? parentPayment.PayerEmail;
        parentPayment.PayerName = $"{customer.FirstName} {customer.LastName}";
        _context.ReservePayments.Update(parentPayment);
    }

    /// <summary>
    /// Asienta en cuenta corriente una compra online que quedó APROBADA (ADR 0009): Charge (+total)
    /// y Payment (−total) juntos, neto 0 en CurrentBalance. Corre en el webhook cuando un pago
    /// Pending (Wallet, o Card in_process) transiciona a Paid; la idempotencia la garantiza el
    /// guard de entrada (pagos ya no-Pending no se reprocesan). Si el pago no tiene Customer
    /// vinculado (pagador no resuelto, ADR 0008) no se asienta: sin cliente no hay cuenta.
    /// </summary>
    private async Task RegisterApprovedOnlinePurchaseAsync(ReservePayment parentPayment, List<Reserve> reserves)
    {
        if (parentPayment.CustomerId is null)
            return;

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == parentPayment.CustomerId.Value);
        if (customer is null)
            return;

        var ordered = reserves.OrderBy(r => r.ReserveDate).ThenBy(r => r.ReserveId).ToList();
        var main = ordered.FirstOrDefault(r => r.ReserveId == parentPayment.ReserveId) ?? ordered.First();
        var ids = string.Join(", ", ordered.Select(r => $"#{r.ReserveId}"));
        var type = ordered.Count > 1 ? "Ida y vuelta" : "Ida";
        var description = $"Reserva online: {type} {ids} - {main.OriginName} - {main.DestinationName}";
        var now = DateTime.UtcNow;

        _context.CustomerAccountTransactions.Add(new CustomerAccountTransaction
        {
            CustomerId = customer.CustomerId,
            Date = now,
            Type = TransactionType.Charge,
            Amount = parentPayment.Amount,
            Description = description,
            RelatedReserveId = parentPayment.ReserveId
        });
        customer.CurrentBalance += parentPayment.Amount;

        _context.CustomerAccountTransactions.Add(new CustomerAccountTransaction
        {
            CustomerId = customer.CustomerId,
            Date = now,
            Type = TransactionType.Payment,
            Amount = -parentPayment.Amount,
            Description = $"Pago online aplicado a {description}",
            RelatedReserveId = parentPayment.ReserveId,
            ReservePaymentId = parentPayment.ReservePaymentId
        });
        customer.CurrentBalance -= parentPayment.Amount;
        _context.Customers.Update(customer);
    }

    private static StatusPaymentEnum? GetPaymentStatusFromExternal(string externalStatusPayment)
    {
        return externalStatusPayment?.ToLower() switch
        {
            "pending" => StatusPaymentEnum.Pending,
            "approved" => StatusPaymentEnum.Paid,
            "authorized" => StatusPaymentEnum.Paid,
            "in_process" => StatusPaymentEnum.Pending,
            "in_mediation" => StatusPaymentEnum.Pending,
            "rejected" => StatusPaymentEnum.Cancelled,
            "cancelled" => StatusPaymentEnum.Cancelled,
            "refunded" => StatusPaymentEnum.Cancelled,
            "charged_back" => StatusPaymentEnum.Cancelled,
            _ => null
        };
    }
}
