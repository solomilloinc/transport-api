using MercadoPago.Client.Common;
using MercadoPago.Client.Payment;
using MercadoPago.Resource.Payment;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq;
using System.Linq.Expressions;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Business.Services.Payment;
using Transport.Domain.Customers;
using Transport.Domain.Customers.Abstraction;
using Transport.Domain.Directions;
using Transport.Domain.Drivers;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Reserves.Abstraction;
using Transport.Domain.Services;
using Transport.Domain.Users;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Customer;
using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Reserve;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Business.ReserveBusiness;

public class ReserveBusiness : IReserveBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMercadoPagoPaymentGateway _paymentGateway;
    private readonly IUserContext _userContext;
    private readonly ICustomerBusiness _customerBusiness;

    public ReserveBusiness(IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IMercadoPagoPaymentGateway paymentGateway,
        ICustomerBusiness customerBusiness)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _paymentGateway = paymentGateway;
        _customerBusiness = customerBusiness;
    }

    public async Task<Result<bool>> CreatePassengerReserves(PassengerReserveCreateRequestWrapperDto passengerReserves)
    {
        // 1) Payer (Customer) desde los items
        var customerId = passengerReserves.Items.FirstOrDefault()?.CustomerId;
        if (customerId is null || customerId == 0)
            return Result.Failure<bool>(Error.Validation(
                "PassengerReserveCreateRequestWrapperDto.CustomerIdRequired",
                "CustomerId is required in at least one passenger item."));

        var payer = await _context.Customers.FindAsync(customerId);
        if (payer is null)
            return Result.Failure<bool>(CustomerError.NotFound);

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            decimal totalExpectedAmount = 0m;

            var mainReserveId = passengerReserves.Items.Min(i => i.ReserveId);
            var servicesCache = new Dictionary<int, Service>();
            var reserveMap = new Dictionary<int, Reserve>();

            // 2) Alta de pasajeros + validaciones
            foreach (var dto in passengerReserves.Items)
            {
                var reserve = await _context.Reserves
                    .Include(r => r.Passengers)
                    .Include(r => r.Driver)
                    .SingleOrDefaultAsync(r => r.ReserveId == dto.ReserveId);

                if (reserve is null)
                    return Result.Failure<bool>(ReserveError.NotFound);

                if (reserve.Status != ReserveStatusEnum.Confirmed)
                    return Result.Failure<bool>(ReserveError.NotAvailable);

                reserveMap[reserve.ReserveId] = reserve;

                if (!servicesCache.TryGetValue(reserve.ServiceId, out var service))
                {
                    service = await _context.Services
                        .Include(s => s.ReservePrices)
                        .Include(s => s.Origin)
                        .Include(s => s.Destination)
                        .SingleOrDefaultAsync(s => s.ServiceId == reserve.ServiceId);

                    if (service is null)
                        return Result.Failure<bool>(ServiceError.ServiceNotFound);

                    servicesCache[reserve.ServiceId] = service;
                }

                var vehicle = await _context.Vehicles.FindAsync(reserve.VehicleId);
                var existingPassengerCount = reserve.Passengers.Count;
                var totalAfterInsert = existingPassengerCount + passengerReserves.Items.Count;

                if (vehicle == null || totalAfterInsert > vehicle.AvailableQuantity)
                    return Result.Failure<bool>(ReserveError.VehicleQuantityNotAvailable(
                        existingPassengerCount, passengerReserves.Items.Count, vehicle?.AvailableQuantity ?? 0));

                var reservePrice = service.ReservePrices
                    .SingleOrDefault(p => p.ReserveTypeId == (ReserveTypeIdEnum)dto.ReserveTypeId);
                if (reservePrice is null || dto.Price != reservePrice.Price)
                    return Result.Failure<bool>(ReserveError.PriceNotAvailable);

                if (reserve.Passengers.Any(p => p.DocumentNumber == dto.DocumentNumber))
                    return Result.Failure<bool>(ReserveError.PassengerAlreadyExists(dto.DocumentNumber));

                var pickupResult = await GetDirectionAsync(dto.PickupLocationId, "Pickup");
                if (pickupResult.IsFailure) return Result.Failure<bool>(pickupResult.Error);

                var dropoffResult = await GetDirectionAsync(dto.DropoffLocationId, "Dropoff");
                if (dropoffResult.IsFailure) return Result.Failure<bool>(dropoffResult.Error);

                // Registramos al pasajero (en admin usamos los datos del payer; ajusta si querés usar los del dto)
                var passenger = new Passenger
                {
                    ReserveId = reserve.ReserveId,
                    FirstName = payer.FirstName,
                    LastName = payer.LastName,
                    DocumentNumber = payer.DocumentNumber,
                    Email = payer.Email,
                    Phone = payer.Phone1,
                    PickupLocationId = dto.PickupLocationId,
                    DropoffLocationId = dto.DropoffLocationId,
                    PickupAddress = pickupResult.Value?.Name,
                    DropoffAddress = dropoffResult.Value?.Name,
                    HasTraveled = dto.HasTraveled,
                    Price = reservePrice.Price,
                    Status = PassengerStatusEnum.Confirmed,
                    CustomerId = payer.CustomerId
                };

                reserve.Passengers.Add(passenger);
                _context.Reserves.Update(reserve);

                totalExpectedAmount += reservePrice.Price;
            }

            var reserveIds = passengerReserves.Items.Select(i => i.ReserveId).Distinct().ToList();
            var description = BuildDescription(reserveIds, reserveMap, servicesCache, passengerReserves);

            // 3) Siempre CHARGE al payer por el total
            _context.CustomerAccountTransactions.Add(new CustomerAccountTransaction
            {
                CustomerId = payer.CustomerId,
                Date = DateTime.UtcNow,
                Type = TransactionType.Charge,
                Amount = totalExpectedAmount,
                Description = description,
                RelatedReserveId = mainReserveId
            });
            payer.CurrentBalance += totalExpectedAmount;
            _context.Customers.Update(payer);

            // 4) Si no hay pagos, guardar y salir
            if (!passengerReserves.Payments.Any())
            {
                await _context.SaveChangesWithOutboxAsync();
                return Result.Success(true);
            }

            var totalProvidedAmount = passengerReserves.Payments.Sum(p => p.TransactionAmount);

            // (Opcional) Enforce igualdad total vs pagos:
            if (totalExpectedAmount != totalProvidedAmount)
                return Result.Failure<bool>(ReserveError.InvalidPaymentAmount(totalExpectedAmount, totalProvidedAmount));

            // Identificar ida/vuelta
            var distinctReserveIds = passengerReserves.Items
      .Select(i => i.ReserveId)
      .Distinct()
      .ToList();

            // Si tenés reserveMap cargado (lo venís armando arriba), ordená por fecha/hora; si faltara, queda por Id
            var orderedReserveIds = distinctReserveIds
                .OrderBy(id => reserveMap.TryGetValue(id, out var r) ? r.ReserveDate : DateTime.MaxValue)
                .ThenBy(id => id)
                .ToList();

            var parentReserveId = orderedReserveIds.First();
            var childReserveIds = orderedReserveIds.Skip(1).ToList();

            // b) Total efectivamente abonado (uno o varios medios)
            var primaryMethod = (PaymentMethodEnum)passengerReserves.Payments.First().PaymentMethod;

            // c) PAGO PADRE (reserva "ida" por orden)
            var parentPayment = new ReservePayment
            {
                ReserveId = parentReserveId,
                CustomerId = payer.CustomerId,
                PayerDocumentNumber = payer.DocumentNumber,
                PayerName = $"{payer.FirstName} {payer.LastName}",
                PayerEmail = payer.Email,
                Amount = totalProvidedAmount,             // total consolidado
                Method = primaryMethod,                   // método “principal” informativo
                Status = StatusPaymentEnum.Paid
            };
            _context.ReservePayments.Add(parentPayment);
            await _context.SaveChangesWithOutboxAsync(); // necesitamos el Id del padre

            // d) Si hay split de medios (>=2), crear hijos de desglose SOLO en la reserva padre
            if (passengerReserves.Payments.Count > 1)
            {
                foreach (var p in passengerReserves.Payments)
                {
                    var breakdownChild = new ReservePayment
                    {
                        ReserveId = parentReserveId,
                        CustomerId = payer.CustomerId,
                        PayerDocumentNumber = payer.DocumentNumber,
                        PayerName = $"{payer.FirstName} {payer.LastName}",
                        PayerEmail = payer.Email,
                        Amount = p.TransactionAmount,                      // importe de ese medio
                        Method = (PaymentMethodEnum)p.PaymentMethod,
                        Status = StatusPaymentEnum.Paid,
                        ParentReservePaymentId = parentPayment.ReservePaymentId
                    };
                    _context.ReservePayments.Add(breakdownChild);
                }
            }

            // e) “Link children” (monto 0) para cada tramo adicional (vuelta, etc.)
            foreach (var childReserveId in childReserveIds)
            {
                var linkChild = new ReservePayment
                {
                    ReserveId = childReserveId,
                    CustomerId = payer.CustomerId,
                    PayerDocumentNumber = payer.DocumentNumber,
                    PayerName = $"{payer.FirstName} {payer.LastName}",
                    PayerEmail = payer.Email,
                    Amount = 0m,                                          // sólo vínculo contable
                    Method = primaryMethod,                               // reutilizamos el del padre
                    Status = StatusPaymentEnum.Paid,
                    ParentReservePaymentId = parentPayment.ReservePaymentId
                };
                _context.ReservePayments.Add(linkChild);
            }

            // Asiento de Payment (negativo) e impacto en saldo
            _context.CustomerAccountTransactions.Add(new CustomerAccountTransaction
            {
                CustomerId = payer.CustomerId,
                Date = DateTime.UtcNow,
                Type = TransactionType.Payment,
                Amount = -totalProvidedAmount,
                Description = $"Pago aplicado a {description}",
                RelatedReserveId = parentReserveId,
                ReservePaymentId = parentPayment.ReservePaymentId
            });
            payer.CurrentBalance -= totalProvidedAmount;
            _context.Customers.Update(payer);

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    public async Task<Result<CreateReserveExternalResult>> CreatePassengerReservesExternal(
        PassengerReserveCreateRequestWrapperExternalDto dto)
    {
        var validationResult = ValidateUserReserveCombination(dto.Items);
        if (validationResult.IsFailure)
            return Result.Failure<CreateReserveExternalResult>(validationResult.Error);

        User userLogged = null;
        Customer bookingCustomer = null;

        if (_userContext.UserId != null && _userContext.UserId > 0)
        {
            userLogged = await _context.Users
                .Include(u => u.Customer)
                .SingleOrDefaultAsync(u => u.UserId == _userContext.UserId);

            bookingCustomer = userLogged?.Customer;
        }

        List<Reserve> reserves = new List<Reserve>();

        return await _unitOfWork.ExecuteInTransactionAsync<CreateReserveExternalResult>(async () =>
        {
            decimal totalExpectedAmount = 0m;

            foreach (var passengerDto in dto.Items)
            {
                var reserve = await _context.Reserves
                   .Include(r => r.Passengers)
                   .SingleOrDefaultAsync(r => r.ReserveId == passengerDto.ReserveId);

                if (reserve is null)
                    return Result.Failure<CreateReserveExternalResult>(ReserveError.NotFound);

                if (reserve.Status != ReserveStatusEnum.Confirmed)
                    return Result.Failure<CreateReserveExternalResult>(ReserveError.NotAvailable);

                var service = await _context.Services
                    .Include(s => s.ReservePrices)
                    .Include(s => s.Origin)
                    .Include(s => s.Destination)
                    .SingleOrDefaultAsync(s => s.ServiceId == reserve.ServiceId);

                var vehicle = await _context.Vehicles.FindAsync(reserve.VehicleId);

                var existingPassengerCount = reserve.Passengers.Count;
                var totalAfterInsert = existingPassengerCount + dto.Items.Count;

                if (totalAfterInsert > vehicle.AvailableQuantity)
                    return Result.Failure<CreateReserveExternalResult>(
                        ReserveError.VehicleQuantityNotAvailable(existingPassengerCount, dto.Items.Count, vehicle.AvailableQuantity));

                var reservePrice = service.ReservePrices
                    .SingleOrDefault(p => p.ReserveTypeId == (ReserveTypeIdEnum)passengerDto.ReserveTypeId);

                if (reservePrice is null)
                    return Result.Failure<CreateReserveExternalResult>(ReserveError.PriceNotAvailable);

                // Verificar si el pasajero ya existe
                if (reserve.Passengers.Any(p => p.DocumentNumber == passengerDto.DocumentNumber))
                    return Result.Failure<CreateReserveExternalResult>(
                        ReserveError.PassengerAlreadyExists(passengerDto.DocumentNumber));

                var pickupResult = await GetDirectionAsync(passengerDto.PickupLocationId, "Pickup");
                if (pickupResult.IsFailure)
                    return Result.Failure<CreateReserveExternalResult>(pickupResult.Error);

                var dropoffResult = await GetDirectionAsync(passengerDto.DropoffLocationId, "Dropoff");
                if (dropoffResult.IsFailure)
                    return Result.Failure<CreateReserveExternalResult>(dropoffResult.Error);

                // Verificar si es cliente existente
                Customer existingCustomer = await _context.Customers
                        .SingleOrDefaultAsync(c => c.DocumentNumber == passengerDto.DocumentNumber);

                var newPassenger = new Passenger
                {
                    ReserveId = reserve.ReserveId,
                    FirstName = passengerDto.FirstName,
                    LastName = passengerDto.LastName,
                    DocumentNumber = passengerDto.DocumentNumber,
                    Email = passengerDto.Email,
                    Phone = passengerDto.Phone1,
                    PickupLocationId = passengerDto.PickupLocationId,
                    DropoffLocationId = passengerDto.DropoffLocationId,
                    PickupAddress = pickupResult.Value?.Name,
                    DropoffAddress = dropoffResult.Value?.Name,
                    HasTraveled = false,
                    Price = reservePrice.Price,
                    Status = dto.Payment is null ? PassengerStatusEnum.PendingPayment : PassengerStatusEnum.Confirmed,
                    CustomerId = existingCustomer?.CustomerId,
                };

                reserve.Passengers.Add(newPassenger);

                _context.Reserves.Update(reserve);
                reserves.Add(reserve);

                totalExpectedAmount += reservePrice.Price;
            }

            if (dto.Payment is null)
            {
                // Crear pago pendiente (padre + hijos link 0 para los otros tramos)
                var resultPayment = await CreatePendingPayment(totalExpectedAmount, reserves, dto.Items.First());
                if (resultPayment.IsFailure)
                    return Result.Failure<CreateReserveExternalResult>(resultPayment.Error);

                string preferenceId = await _paymentGateway.CreatePreferenceAsync(
                    resultPayment.Value.ToString(),
                    totalExpectedAmount,
                    dto.Items
                );

                await _context.SaveChangesWithOutboxAsync();
                return Result.Success(new CreateReserveExternalResult(PaymentStatus.Pending, preferenceId));
            }
            else
            {
                var totalProvidedAmount = dto.Payment.TransactionAmount;

                if (totalExpectedAmount != totalProvidedAmount)
                    return Result.Failure<CreateReserveExternalResult>(
                        ReserveError.InvalidPaymentAmount(totalExpectedAmount, totalProvidedAmount));

                // Crear pago (padre + hijos link 0) usando orden de reservas
                var resultPayment = await CreatePayment(dto.Payment, reserves);
                if (resultPayment.IsFailure)
                    return Result.Failure<CreateReserveExternalResult>(resultPayment.Error);

                await _context.SaveChangesWithOutboxAsync();
                return Result.Success(new CreateReserveExternalResult(PaymentStatus.Approved, null));
            }
        });
    }

    private async Task<Result<bool>> CreatePayment(CreatePaymentExternalRequestDto paymentData, List<Reserve> reserves)
    {
        // Ordenar reservas para decidir padre/hijos sin depender de ReserveTypeId
        var orderedReserves = reserves
            .OrderBy(r => r.ReserveDate)
            .ThenBy(r => r.ReserveId)
            .ToList();

        var mainReserve = orderedReserves.First();

        // Determinar pagador (si es Customer) por DNI
        var payingCustomer = await _context.Customers
            .SingleOrDefaultAsync(c => c.DocumentNumber == paymentData.IdentificationNumber);

        // 1) Crear PAGO PADRE en Pending para obtener Id (lo usamos como ExternalReference)
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
            CreatedBy = "System",
            CreatedDate = DateTime.UtcNow
        };

        _context.ReservePayments.Add(parentPayment);
        await _context.SaveChangesWithOutboxAsync(); // para obtener el Id del padre

        // 2) Llamada a MP con ExternalReference = Id del padre
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
                    Number = paymentData.IdentificationNumber
                }
            }
        };

        var result = await _paymentGateway.CreatePaymentAsync(paymentRequest);

        var statusPaymentInternal = GetPaymentStatusFromExternal(result.Status);
        if (statusPaymentInternal is null)
        {
            return Result.Failure<bool>(
                Error.Validation("Payment.StatusMappingError",
                    $"El estado de pago externo '{result.Status}' no pudo ser interpretado correctamente.")
            );
        }

        // 3) Actualizar PADRE con datos reales del gateway
        parentPayment.PaymentExternalId = result.Id;
        parentPayment.Status = statusPaymentInternal.Value;
        parentPayment.StatusDetail = result.StatusDetail;
        parentPayment.ResultApiExternalRawJson = JsonConvert.SerializeObject(result);
        parentPayment.UpdatedBy = "System";
        parentPayment.UpdatedDate = DateTime.UtcNow;
        _context.ReservePayments.Update(parentPayment);

        // 4) Crear HIJOS “link” (monto 0) para cada reserva adicional (vuelta, etc.)
        foreach (var extraReserve in orderedReserves.Skip(1))
        {
            var child = new ReservePayment
            {
                ReserveId = extraReserve.ReserveId,
                CustomerId = payingCustomer?.CustomerId,
                PayerDocumentNumber = parentPayment.PayerDocumentNumber,
                PayerName = parentPayment.PayerName,
                PayerEmail = parentPayment.PayerEmail,
                Amount = 0m,
                Method = PaymentMethodEnum.Online,
                Status = parentPayment.Status, // espejo del padre
                StatusDetail = parentPayment.StatusDetail,
                ParentReservePaymentId = parentPayment.ReservePaymentId,
                CreatedBy = "System",
                CreatedDate = DateTime.UtcNow
            };
            _context.ReservePayments.Add(child);
        }

        // 5) Estado de pasajeros según resultado
        var allPassengers = orderedReserves.SelectMany(r => r.Passengers).ToList();
        var isPendingApproval = result.Status == "pending" || result.Status == "in_process";

        var passengerStatus = isPendingApproval
            ? PassengerStatusEnum.PendingPayment
            : statusPaymentInternal == StatusPaymentEnum.Paid
                ? PassengerStatusEnum.Confirmed
                : PassengerStatusEnum.Cancelled;

        foreach (var p in allPassengers)
        {
            p.Status = passengerStatus;
            p.UpdatedBy = "System";
            p.UpdatedDate = DateTime.UtcNow;
            _context.Passengers.Update(p);
        }

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }


    private async Task<Result<int>> CreatePendingPayment(
      decimal amount,
      List<Reserve> reserves,
      PassengerReserveCreateRequestDto firstPassenger)
    {
        // Ordenar reservas para decidir padre/hijos
        var orderedReserves = reserves
            .OrderBy(r => r.ReserveDate)
            .ThenBy(r => r.ReserveId)
            .ToList();

        var mainReserve = orderedReserves.First();

        // (Opcional) si el primer pasajero es cliente
        var payingCustomer = await _context.Customers
            .FirstOrDefaultAsync(c => c.DocumentNumber == firstPassenger.DocumentNumber);

        // Padre pending
        var parentPayment = new ReservePayment
        {
            Amount = amount,
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
            CreatedBy = "System",
            CreatedDate = DateTime.UtcNow
        };

        _context.ReservePayments.Add(parentPayment);
        await _context.SaveChangesWithOutboxAsync(); // obtener Id del padre

        // Hijos pending (monto 0) para reservas adicionales
        foreach (var extraReserve in orderedReserves.Skip(1))
        {
            var child = new ReservePayment
            {
                ReserveId = extraReserve.ReserveId,
                CustomerId = payingCustomer?.CustomerId,
                PayerDocumentNumber = parentPayment.PayerDocumentNumber,
                PayerName = parentPayment.PayerName,
                PayerEmail = parentPayment.PayerEmail,
                Amount = 0m,
                Method = PaymentMethodEnum.Online,
                Status = StatusPaymentEnum.Pending,
                StatusDetail = "wallet_pending",
                ParentReservePaymentId = parentPayment.ReservePaymentId,
                CreatedBy = "System",
                CreatedDate = DateTime.UtcNow
            };
            _context.ReservePayments.Add(child);
        }

        await _context.SaveChangesWithOutboxAsync();

        // Devolvemos el ID del padre para usarlo como ExternalReference
        return Result.Success(parentPayment.ReservePaymentId);
    }



    private string BuildDescription(List<int> reserveIds, Dictionary<int, Reserve> reserveMap,
        Dictionary<int, Service> servicesCache, PassengerReserveCreateRequestWrapperDto passengerReserves)
    {
        if (reserveIds.Count == 1)
        {
            var rid = reserveIds[0];
            var reserve = reserveMap[rid];
            var service = servicesCache[reserve.ServiceId];
            var type = passengerReserves.Items.First(i => i.ReserveId == rid).ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta
                ? "Ida y vuelta"
                : "Ida";
            return $"Reserva: {type} #{rid} - {service.Origin.Name} - {service.Destination.Name} {reserve.ReserveDate:HH:mm}";
        }

        var rid1 = reserveIds[0];
        var rid2 = reserveIds[1];
        var reserve1 = reserveMap[rid1];
        var reserve2 = reserveMap[rid2];
        var service1 = servicesCache[reserve1.ServiceId];
        var service2 = servicesCache[reserve2.ServiceId];

        var desc1 = $"Ida #{rid1} - {service1.Origin.Name} - {service1.Destination.Name} {reserve1.ReserveDate:HH:mm}";
        var desc2 = $"Vuelta #{rid2} - {service2.Destination.Name} - {service2.Origin.Name} {reserve2.ReserveDate:HH:mm}";

        return $"Reserva(s): {desc1}; {desc2}";
    }

    private Result ValidateUserReserveCombination(List<PassengerReserveCreateRequestDto> items)
    {
        var distinctReserveIds = items.Select(x => x.ReserveId).Distinct().ToList();
        if (distinctReserveIds.Count > 2)
            return Result.Failure(ReserveError.InvalidReserveCombination("Solo se permite reservar hasta 2 reservas: ida y vuelta."));

        var types = items.Select(x => (ReserveTypeIdEnum)x.ReserveTypeId).ToList();

        if (types.Count > 2)
            return Result.Failure(ReserveError.InvalidReserveCombination("Solo se permite reservar como máximo ida y vuelta."));

        if (types.Count == 2 && !(types.Contains(ReserveTypeIdEnum.Ida) && types.Contains(ReserveTypeIdEnum.IdaVuelta)))
            return Result.Failure(ReserveError.InvalidReserveCombination("La combinación válida es Ida + IdaVuelta únicamente."));

        if (types.Count == 1 && types.First() == ReserveTypeIdEnum.IdaVuelta)
            return Result.Failure(ReserveError.InvalidReserveCombination("No se puede reservar únicamente la vuelta sin haber reservado ida."));

        return Result.Success();
    }

    private async Task<Result<Direction?>> GetDirectionAsync(int? locationId, string type)
    {
        if (locationId is null || locationId == 0)
            return Result.Success<Direction?>(null);

        var direction = await _context.Directions.FindAsync(locationId);

        if (direction is null)
        {
            return Result.Failure<Direction?>(
                Error.NotFound(
                    $"Direction.{type}NotFound",
                    $"{type} direction not found"
                )
            );
        }

        return Result.Success<Direction?>(direction);
    }

    private StatusPaymentEnum? GetPaymentStatusFromExternal(string externalStatusPayment)
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

    public async Task<Result<bool>> UpdateReservePaymentsByExternalId(string externalPaymentId)
    {
        // Si ya se actualizó, salir rápido
        if (_context.ReservePayments.Any(p => p.PaymentExternalId == long.Parse(externalPaymentId)
            && p.Status != StatusPaymentEnum.Pending))
        {
            return Result.Success(true);
        }

        Payment mpPayment = await _paymentGateway.GetPaymentAsync(externalPaymentId);

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // ExternalReference = ReservePaymentId (del padre)
            var parentPayment = await _context.ReservePayments
                .FirstOrDefaultAsync(rp => rp.ReservePaymentId == int.Parse(mpPayment.ExternalReference));

            if (parentPayment == null)
                return Result.Failure<bool>(Error.NotFound("Payment.NotFound",
                    "No se encontró el pago con el ID externo proporcionado"));

            var internalStatus = GetPaymentStatusFromExternal(mpPayment.Status);
            if (internalStatus == null)
                return Result.Failure<bool>(Error.Validation("Payment.InvalidStatus",
                    "Estado de pago no reconocido"));

            // Actualizar padre
            parentPayment.Status = internalStatus.Value;
            parentPayment.StatusDetail = mpPayment.StatusDetail;
            parentPayment.PaymentExternalId = mpPayment.Id;
            parentPayment.ResultApiExternalRawJson = JsonConvert.SerializeObject(mpPayment);
            parentPayment.UpdatedBy = "System";
            parentPayment.UpdatedDate = DateTime.UtcNow;
            _context.ReservePayments.Update(parentPayment);

            // Actualizar hijos (mismo estado que el padre)
            var children = await _context.ReservePayments
                .Where(c => c.ParentReservePaymentId == parentPayment.ReservePaymentId)
                .ToListAsync();

            foreach (var child in children)
            {
                child.Status = parentPayment.Status;
                child.StatusDetail = parentPayment.StatusDetail;
                child.UpdatedBy = "System";
                child.UpdatedDate = DateTime.UtcNow;
                _context.ReservePayments.Update(child);
            }

            // Actualizar pasajeros de la(s) reserva(s) del padre y de cada hijo
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
                    passenger.UpdatedBy = "System";
                    passenger.UpdatedDate = DateTime.UtcNow;
                    _context.Passengers.Update(passenger);
                }
            }

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    // Métodos de reportes actualizados
    public async Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>> GetReserveReport(
        DateTime reserveDate, PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto)
    {
        var query = _context.Reserves
            .Include(r => r.Passengers)
            .ThenInclude(p => p.Customer)
            .Include(r => r.Service)
                .ThenInclude(s => s.Vehicle)
            .Include(r => r.Service)
                .ThenInclude(s => s.ReservePrices)
            .Where(rp => rp.Status == ReserveStatusEnum.Confirmed);

        var date = reserveDate.Date;
        query = query.Where(rp => rp.ReserveDate.Date == date);

        var sortMappings = new Dictionary<string, Expression<Func<Reserve, object>>>
        {
            ["reservedate"] = rp => rp.ReserveDate,
            ["serviceorigin"] = rp => rp.Service.Origin.Name,
            ["servicedest"] = rp => rp.Service.Destination.Name,
        };


        var pagedResult = await query.ToPagedReportAsync<ReserveReportResponseDto, Reserve, ReserveReportFilterRequestDto>(
                   requestDto,
                   selector: rp => new ReserveReportResponseDto(
                       rp.ReserveId,
                       rp.OriginName,
                       rp.DestinationName,
                       rp.Service.Vehicle.AvailableQuantity,
                       rp.Passengers.Count,
                       rp.DepartureHour.ToString(@"hh\:mm"),
                       rp.VehicleId,
                       rp.DriverId.GetValueOrDefault(),
                       rp.Passengers
                         .Select(p => new PassengerReserveReportResponseDto(
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
                             p.Customer != null ? p.Customer.CurrentBalance : 0))
                         .ToList(),
                       rp.Service.ReservePrices.Select(p => new ReservePriceReport((int)p.ReserveTypeId, p.Price)).ToList()
                   ),
                   sortMappings: sortMappings
               );

        return Result.Success(pagedResult);
    }

    public async Task<Result<ReserveGroupedPagedReportResponseDto>> GetReserveReport(
        PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto)
    {
        var idaDate = requestDto.Filters.DepartureDate.Date;
        var vueltaDate = requestDto.Filters.ReturnDate?.Date;
        var passengersRequested = requestDto.Filters.Passengers;

        var originId = requestDto.Filters.OriginId;
        var destinationId = requestDto.Filters.DestinationId;

        var query = _context.Reserves
            .Include(r => r.Service)
                .ThenInclude(s => s.Origin)
            .Include(r => r.Service)
                .ThenInclude(s => s.Destination)
            .Include(r => r.Service.ReservePrices)
            .Include(r => r.Service.Vehicle)
            .Include(r => r.Passengers)
            .Where(rp => rp.Status == ReserveStatusEnum.Confirmed &&
                        (rp.ReserveDate.Date == idaDate ||
                         (vueltaDate.HasValue && rp.ReserveDate.Date == vueltaDate.Value)));

        var items = await query.ToListAsync();

        var idaItems = items
            .Where(rp => rp.ReserveDate.Date == idaDate)
            .Where(rp => rp.Service.Origin.CityId == originId && rp.Service.Destination.CityId == destinationId)
            .Where(rp =>
            {
                int totalReserved = rp.Passengers
                    .Count(p => p.Status == PassengerStatusEnum.Confirmed
                             || p.Status == PassengerStatusEnum.PendingPayment);
                var available = rp.Service.Vehicle.AvailableQuantity - totalReserved;
                return available >= passengersRequested;
            })
            .Select(rp => new ReserveExternalReportResponseDto(
                rp.ReserveId,
                rp.Service.Origin.Name,
                rp.Service.Destination.Name,
                rp.DepartureHour.ToString(@"hh\:mm"),
                rp.Service.ReservePrices.FirstOrDefault(p => p.ReserveTypeId == ReserveTypeIdEnum.Ida)?.Price ?? 0
            ))
            .ToList();

        var vueltaItems = vueltaDate.HasValue
            ? items
                .Where(rp => rp.ReserveDate.Date == vueltaDate.Value)
                .Where(rp => rp.Service.Origin.CityId == destinationId && rp.Service.Destination.CityId == originId)
                .Where(rp =>
                {
                    int totalReserved = rp.Passengers
                        .Count(p => p.Status == PassengerStatusEnum.Confirmed
                                 || p.Status == PassengerStatusEnum.PendingPayment);
                    var available = rp.Service.Vehicle.AvailableQuantity - totalReserved;
                    return available >= passengersRequested;
                })
                .Select(rp => new ReserveExternalReportResponseDto(
                    rp.ReserveId,
                    rp.Service.Origin.Name,
                    rp.Service.Destination.Name,
                    rp.DepartureHour.ToString(@"hh\:mm"),
                    rp.Service.ReservePrices.FirstOrDefault(p => p.ReserveTypeId == ReserveTypeIdEnum.Ida)?.Price ?? 0
                ))
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

        var sortMappings = new Dictionary<string, Expression<Func<Passenger, object>>>
        {
            ["passengerfullname"] = p => p.FirstName + " " + p.LastName,
            ["documentnumber"] = p => p.DocumentNumber,
            ["email"] = p => p.Email
        };

        var pagedResult = await query.ToPagedReportAsync<PassengerReserveReportResponseDto, Passenger, PassengerReserveReportFilterRequestDto>(
            requestDto,
            selector: p => new PassengerReserveReportResponseDto(
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
                p.Customer != null ? p.Customer.CurrentBalance : 0),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> UpdateReserveAsync(int reserveId, ReserveUpdateRequestDto request)
    {
        var reserve = await _context.Reserves
            .FirstOrDefaultAsync(r => r.ReserveId == reserveId);

        if (reserve is null)
            return Result.Failure<bool>(ReserveError.NotFound);

        if (request.VehicleId != null)
        {
            var vehicle = await _context.Vehicles.FindAsync(request.VehicleId);
            if (vehicle is null)
                return Result.Failure<bool>(VehicleError.VehicleNotFound);
            reserve.VehicleId = request.VehicleId.Value;
        }

        if (request.DriverId != null)
        {
            var driver = await _context.Drivers.FindAsync(request.DriverId);
            if (driver is null)
                return Result.Failure<bool>(DriverError.DriverNotFound);
            reserve.DriverId = request.DriverId.Value;
        }

        reserve.ReserveDate = request.ReserveDate ?? reserve.ReserveDate;
        reserve.DepartureHour = request.DepartureHour ?? reserve.DepartureHour;

        if (request.Status != null)
        {
            reserve.Status = (ReserveStatusEnum)request.Status;
        }

        _context.Reserves.Update(reserve);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdatePassengerReserveAsync(int passengerId, PassengerReserveUpdateRequestDto request)
    {
        var passenger = await _context.Passengers
            .SingleOrDefaultAsync(p => p.PassengerId == passengerId);

        if (passenger == null)
            return Result.Failure<bool>(PassengerError.NotFound);

        if (request.PickupLocationId.HasValue)
        {
            var pickup = await _context.Directions.FindAsync(request.PickupLocationId);
            if (pickup == null)
                return Result.Failure<bool>(Error.NotFound("Pickup.NotFound", "Pickup location not found"));

            passenger.PickupLocationId = pickup.DirectionId;
            passenger.PickupAddress = pickup.Name;
        }

        if (request.DropoffLocationId.HasValue)
        {
            var dropoff = await _context.Directions.FindAsync(request.DropoffLocationId);
            if (dropoff == null)
                return Result.Failure<bool>(Error.NotFound("Dropoff.NotFound", "Dropoff location not found"));

            passenger.DropoffLocationId = dropoff.DirectionId;
            passenger.DropoffAddress = dropoff.Name;
        }

        if (request.HasTraveled.HasValue)
        {
            passenger.HasTraveled = request.HasTraveled.Value;
        }

        _context.Passengers.Update(passenger);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<PagedReportResponseDto<ReservePriceReportResponseDto>>> GetReservePriceReport(
        PagedReportRequestDto<ReservePriceReportFilterRequestDto> requestDto)
    {
        var query = _context.ReservePrices
            .AsNoTracking()
            .Include(rp => rp.Service)
            .Where(rp => rp.Status == EntityStatusEnum.Active)
            .AsQueryable();

        if (requestDto.Filters?.ReserveTypeId is not null)
            query = query.Where(rp => (int)rp.ReserveTypeId == requestDto.Filters.ReserveTypeId);

        if (requestDto.Filters?.ServiceId is not null)
            query = query.Where(rp => rp.ServiceId == requestDto.Filters.ServiceId);

        if (requestDto.Filters?.PriceFrom is not null)
            query = query.Where(rp => rp.Price >= requestDto.Filters.PriceFrom);

        if (requestDto.Filters?.PriceTo is not null)
            query = query.Where(rp => rp.Price <= requestDto.Filters.PriceTo);

        var sortMappings = new Dictionary<string, Expression<Func<ReservePrice, object>>>
        {
            ["reservepriceid"] = rp => rp.ReservePriceId,
            ["serviceid"] = rp => rp.ServiceId,
            ["servicename"] = rp => rp.Service.Name,
            ["price"] = rp => rp.Price,
            ["reservetypeid"] = rp => rp.ReserveTypeId
        };

        var pagedResult = await query.ToPagedReportAsync<ReservePriceReportResponseDto, ReservePrice, ReservePriceReportFilterRequestDto>(
            requestDto,
            selector: rp => new ReservePriceReportResponseDto(
                rp.ReservePriceId,
                rp.ServiceId,
                rp.Service.Name,
                rp.Price,
                (int)rp.ReserveTypeId
            ),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
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
                    .ThenInclude(s => s.ReservePrices)
                .FirstOrDefaultAsync(r => r.ReserveId == reserveId);

            if (reserve is null)
                return Result.Failure<bool>(ReserveError.NotFound);

            if (payments == null || !payments.Any())
                return Result.Failure<bool>(Error.Validation("Payments.Empty",
                    "Debe proporcionar al menos un pago."));

            // Validar montos
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

            // Validar métodos duplicados
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

            // Calcular el monto esperado basado en los pasajeros
            var expectedAmount = reserve.Passengers.Sum(p => p.Price);
            var providedAmount = payments.Sum(p => p.TransactionAmount);

            if (expectedAmount != providedAmount)
                return Result.Failure<bool>(
                    ReserveError.InvalidPaymentAmount(expectedAmount, providedAmount));

            // Determinar quien paga usando el primer pasajero
            var firstPayment = payments.First();

            Customer payer = null;
            payer = await _context.Customers
                   .FindAsync(customerId);

            if (payer == null)
            {
                return Result.Failure<bool>(CustomerError.NotFound);
            }

            foreach (var payment in payments)
            {
                var newPayment = new ReservePayment
                {
                    ReserveId = reserveId,
                    CustomerId = payer?.CustomerId,
                    Amount = payment.TransactionAmount,
                    Method = (PaymentMethodEnum)payment.PaymentMethod,
                    Status = StatusPaymentEnum.Paid,
                };

                _context.ReservePayments.Add(newPayment);
            }

            // Actualizar estado de los pasajeros a confirmado
            foreach (var passenger in reserve.Passengers)
            {
                if (passenger.Status == PassengerStatusEnum.PendingPayment)
                {
                    passenger.Status = PassengerStatusEnum.Confirmed;
                    _context.Passengers.Update(passenger);
                }
            }

            var transaction = new CustomerAccountTransaction
            {
                CustomerId = payer.CustomerId,
                Date = DateTime.UtcNow,
                Type = TransactionType.Payment,
                Amount = -providedAmount,
                Description = $"Pago de reserva #{reserveId}",
                RelatedReserveId = reserveId
            };
            _context.CustomerAccountTransactions.Add(transaction);

            payer.CurrentBalance -= providedAmount;
            _context.Customers.Update(payer);

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }
}