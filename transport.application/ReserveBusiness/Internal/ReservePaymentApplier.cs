using MercadoPago.Client.Common;
using MercadoPago.Client.Payment;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Transport.Business.Data;
using Transport.Business.Services.Payment;
using Transport.Domain.CashBoxes.Abstraction;
using Transport.Domain.Customers;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Customer;
using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Internal;

/// <summary>
/// Aplica el pago sobre las reservas/pasajeros recién creados. Tres caminos:
/// admin (cash + cuenta corriente con padre/hijos por split de medios),
/// externo con token MP (CreatePaymentAsync) y externo sin token (preference MP).
/// Comparte primitivas internas (parent payment, breakdown children, status update).
/// </summary>
internal sealed class ReservePaymentApplier
{
    private readonly IApplicationDbContext _context;
    private readonly ICashBoxBusiness _cashBoxBusiness;
    private readonly IMercadoPagoPaymentGateway _paymentGateway;

    public ReservePaymentApplier(
        IApplicationDbContext context,
        ICashBoxBusiness cashBoxBusiness,
        IMercadoPagoPaymentGateway paymentGateway)
    {
        _context = context;
        _cashBoxBusiness = cashBoxBusiness;
        _paymentGateway = paymentGateway;
    }

    public async Task<Result<bool>> ApplyAdminAsync(
        PassengerReserveCreateRequestWrapperDto request,
        IReadOnlyList<EnrichedPassengerItem> enriched,
        Customer payer,
        decimal totalExpected,
        int mainReserveId)
    {
        var reserveMap = enriched
            .GroupBy(e => e.ReserveId)
            .ToDictionary(g => g.Key, g => g.First().Reserve);
        var reserveIds = reserveMap.Keys.ToList();
        var description = BuildAdminDescription(reserveIds, reserveMap, request);

        // 1) CHARGE al payer por el total esperado
        _context.CustomerAccountTransactions.Add(new CustomerAccountTransaction
        {
            CustomerId = payer.CustomerId,
            Date = DateTime.UtcNow,
            Type = TransactionType.Charge,
            Amount = totalExpected,
            Description = description,
            RelatedReserveId = mainReserveId,
        });
        payer.CurrentBalance += totalExpected;
        _context.Customers.Update(payer);

        // 2) Si no hay pagos, salimos acá (pasajeros quedan PendingPayment)
        if (!request.Payments.Any())
        {
            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        }

        var totalProvided = request.Payments.Sum(p => p.TransactionAmount);
        if (totalProvided > totalExpected)
            return Result.Failure<bool>(ReserveError.OverPaymentNotAllowed(totalExpected, totalProvided));

        var cashBoxResult = await _cashBoxBusiness.GetOpenCashBoxEntity();
        if (cashBoxResult.IsFailure)
            return Result.Failure<bool>(cashBoxResult.Error);

        var cashBox = cashBoxResult.Value;
        var primaryMethod = (PaymentMethodEnum)request.Payments.First().PaymentMethod;

        var parentPayment = new ReservePayment
        {
            ReserveId = mainReserveId,
            CustomerId = payer.CustomerId,
            PayerDocumentNumber = payer.DocumentNumber,
            PayerName = $"{payer.FirstName} {payer.LastName}",
            PayerEmail = payer.Email,
            Amount = totalProvided,
            Method = primaryMethod,
            Status = StatusPaymentEnum.Paid,
            StatusDetail = "paid_on_departure",
            CashBoxId = cashBox.CashBoxId,
        };
        _context.ReservePayments.Add(parentPayment);
        await _context.SaveChangesWithOutboxAsync(); // necesitamos el ID del padre

        if (request.Payments.Count > 1)
        {
            foreach (var p in request.Payments)
            {
                _context.ReservePayments.Add(new ReservePayment
                {
                    ReserveId = mainReserveId,
                    CustomerId = payer.CustomerId,
                    PayerDocumentNumber = payer.DocumentNumber,
                    PayerName = $"{payer.FirstName} {payer.LastName}",
                    PayerEmail = payer.Email,
                    Amount = p.TransactionAmount,
                    Method = (PaymentMethodEnum)p.PaymentMethod,
                    Status = StatusPaymentEnum.Paid,
                    ParentReservePaymentId = parentPayment.ReservePaymentId,
                    CashBoxId = cashBox.CashBoxId,
                });
            }
        }

        var paymentDescription = totalProvided < totalExpected
            ? $"Pago parcial aplicado a {description}"
            : $"Pago aplicado a {description}";

        _context.CustomerAccountTransactions.Add(new CustomerAccountTransaction
        {
            CustomerId = payer.CustomerId,
            Date = DateTime.UtcNow,
            Type = TransactionType.Payment,
            Amount = -totalProvided,
            Description = paymentDescription,
            RelatedReserveId = mainReserveId,
            ReservePaymentId = parentPayment.ReservePaymentId,
        });
        payer.CurrentBalance -= totalProvided;
        _context.Customers.Update(payer);

        if (totalProvided >= totalExpected)
        {
            foreach (var reserve in reserveMap.Values)
            {
                foreach (var p in reserve.Passengers)
                {
                    if (p.Status == PassengerStatusEnum.PendingPayment)
                    {
                        p.Status = PassengerStatusEnum.Confirmed;
                        _context.Passengers.Update(p);
                    }
                }
            }
        }

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> ApplyExternalWithTokenAsync(
        CreatePaymentExternalRequestDto paymentData,
        List<Reserve> reserves)
    {
        var orderedReserves = reserves
            .OrderBy(r => r.ReserveDate)
            .ThenBy(r => r.ReserveId)
            .ToList();
        var mainReserve = orderedReserves.First();

        var payingCustomer = await _context.Customers
            .SingleOrDefaultAsync(c => c.DocumentNumber == paymentData.IdentificationNumber);

        var cashBoxResult = await _cashBoxBusiness.GetOpenCashBoxEntity();
        var cashBoxId = cashBoxResult.IsSuccess ? cashBoxResult.Value.CashBoxId : (int?)null;

        var parentPayment = new ReservePayment
        {
            Amount = paymentData.TransactionAmount,
            ReserveId = mainReserve.ReserveId,
            CustomerId = payingCustomer?.CustomerId,
            PayerDocumentNumber = paymentData.IdentificationNumber,
            PayerName = payingCustomer != null
                ? $"{payingCustomer.FirstName} {payingCustomer.LastName}"
                : paymentData.PayerEmail,
            PayerEmail = paymentData.PayerEmail,
            Method = PaymentMethodEnum.Online,
            Status = StatusPaymentEnum.Pending,
            StatusDetail = "creating",
            CashBoxId = cashBoxId,
        };
        _context.ReservePayments.Add(parentPayment);
        await _context.SaveChangesWithOutboxAsync(); // necesitamos el ID del padre

        var paymentRequest = new PaymentCreateRequest
        {
            TransactionAmount = paymentData.TransactionAmount,
            Token = paymentData.Token,
            Description = paymentData.Description,
            Installments = paymentData.Installments,
            PaymentMethodId = paymentData.PaymentMethodId,
            ExternalReference = parentPayment.ReservePaymentId.ToString(),
            Payer = new PaymentPayerRequest
            {
                Email = paymentData.PayerEmail,
                Identification = new IdentificationRequest
                {
                    Type = paymentData.IdentificationType,
                    Number = paymentData.IdentificationNumber,
                },
            },
        };

        var result = await _paymentGateway.CreatePaymentAsync(paymentRequest);

        var statusInternal = MapExternalPaymentStatus(result.Status);
        if (statusInternal is null)
        {
            return Result.Failure<bool>(Error.Validation(
                "Payment.StatusMappingError",
                $"El estado de pago externo '{result.Status}' no pudo ser interpretado correctamente."));
        }

        parentPayment.PaymentExternalId = result.Id;
        parentPayment.Status = statusInternal.Value;
        parentPayment.StatusDetail = result.StatusDetail;
        parentPayment.ResultApiExternalRawJson = JsonConvert.SerializeObject(result);
        _context.ReservePayments.Update(parentPayment);

        var allPassengers = orderedReserves.SelectMany(r => r.Passengers).ToList();
        var isPendingApproval = result.Status == "pending" || result.Status == "in_process";

        var passengerStatus = isPendingApproval
            ? PassengerStatusEnum.PendingPayment
            : statusInternal == StatusPaymentEnum.Paid
                ? PassengerStatusEnum.Confirmed
                : PassengerStatusEnum.Cancelled;

        foreach (var p in allPassengers)
        {
            p.Status = passengerStatus;
            _context.Passengers.Update(p);
        }

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<string>> ApplyExternalPendingAsync(
        decimal totalAmount,
        List<Reserve> reserves,
        PassengerReserveExternalCreateRequestDto firstPassenger,
        List<PassengerReserveExternalCreateRequestDto> mpItems)
    {
        var orderedReserves = reserves
            .OrderBy(r => r.ReserveDate)
            .ThenBy(r => r.ReserveId)
            .ToList();
        var mainReserve = orderedReserves.First();

        var payingCustomer = await _context.Customers
            .FirstOrDefaultAsync(c => c.DocumentNumber == firstPassenger.DocumentNumber);

        var cashBoxResult = await _cashBoxBusiness.GetOpenCashBoxEntity();
        var cashBoxId = cashBoxResult.IsSuccess ? cashBoxResult.Value.CashBoxId : (int?)null;

        var parentPayment = new ReservePayment
        {
            Amount = totalAmount,
            ReserveId = mainReserve.ReserveId,
            CustomerId = payingCustomer?.CustomerId,
            PayerDocumentNumber = firstPassenger.DocumentNumber,
            PayerName = payingCustomer != null
                ? $"{payingCustomer.FirstName} {payingCustomer.LastName}"
                : $"{firstPassenger.FirstName} {firstPassenger.LastName}",
            PayerEmail = payingCustomer?.Email ?? firstPassenger.Email,
            Method = PaymentMethodEnum.Online,
            Status = StatusPaymentEnum.Pending,
            StatusDetail = "wallet_pending",
            ResultApiExternalRawJson = null,
            CashBoxId = cashBoxId,
        };
        _context.ReservePayments.Add(parentPayment);
        await _context.SaveChangesWithOutboxAsync();

        var preferenceId = await _paymentGateway.CreatePreferenceAsync(
            parentPayment.ReservePaymentId.ToString(),
            totalAmount,
            mpItems);

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(preferenceId);
    }

    private static StatusPaymentEnum? MapExternalPaymentStatus(string externalStatus)
    {
        return externalStatus?.ToLower() switch
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
            _ => null,
        };
    }

    /// <summary>
    /// Construye la descripción contable que va al CustomerAccountTransaction.
    /// Mantiene el formato exacto del método original BuildDescription para
    /// no alterar lo que ven los reportes.
    /// </summary>
    private static string BuildAdminDescription(
        List<int> reserveIds,
        Dictionary<int, Reserve> reserveMap,
        PassengerReserveCreateRequestWrapperDto request)
    {
        if (reserveIds.Count == 1)
        {
            var rid = reserveIds[0];
            var reserve = reserveMap[rid];
            var originName = reserve.Service?.Trip.OriginCity.Name ?? reserve.OriginName;
            var destName = reserve.Service?.Trip.DestinationCity.Name ?? reserve.DestinationName;
            var type = request.Items.First(i => i.ReserveId == rid).ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta
                ? "Ida y vuelta"
                : "Ida";
            return $"Reserva: {type} #{rid} - {originName} - {destName} {reserve.ReserveDate:HH:mm}";
        }

        var rid1 = reserveIds[0];
        var rid2 = reserveIds[1];
        var reserve1 = reserveMap[rid1];
        var reserve2 = reserveMap[rid2];

        var origin1 = reserve1.Service?.Trip.OriginCity.Name ?? reserve1.OriginName;
        var dest1 = reserve1.Service?.Trip.DestinationCity.Name ?? reserve1.DestinationName;
        var origin2 = reserve2.Service?.Trip.OriginCity.Name ?? reserve2.OriginName;
        var dest2 = reserve2.Service?.Trip.DestinationCity.Name ?? reserve2.DestinationName;

        var desc1 = $"Ida #{rid1} - {origin1} - {dest1} {reserve1.ReserveDate:HH:mm}";
        var desc2 = $"Vuelta #{rid2} - {dest2} - {origin2} {reserve2.ReserveDate:HH:mm}";

        return $"Reserva(s): {desc1}; {desc2}";
    }
}
