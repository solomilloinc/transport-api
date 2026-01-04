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
using Transport.SharedKernel.Contracts.Payment;
using Transport.SharedKernel.Contracts.Reserve;
using Transport.SharedKernel.Contracts.Service;
using Transport.SharedKernel.Configuration;

namespace Transport.Business.ReserveBusiness;

public class ReserveBusiness : IReserveBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMercadoPagoPaymentGateway _paymentGateway;
    private readonly IUserContext _userContext;
    private readonly ICustomerBusiness _customerBusiness;
    private readonly IReserveOption _reserveOptions;

    public ReserveBusiness(IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IMercadoPagoPaymentGateway paymentGateway,
        ICustomerBusiness customerBusiness,
        IReserveOption reserveOptions)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _paymentGateway = paymentGateway;
        _customerBusiness = customerBusiness;
        _reserveOptions = reserveOptions;
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

                var pickupResult = await GetDirectionAsync(dto.PickupLocationId, "Pickup");
                if (pickupResult.IsFailure) return Result.Failure<bool>(pickupResult.Error);

                var dropoffResult = await GetDirectionAsync(dto.DropoffLocationId, "Dropoff");
                if (dropoffResult.IsFailure) return Result.Failure<bool>(dropoffResult.Error);
                var passenger = new Passenger
                {
                    ReserveId = reserve.ReserveId,
                    PickupLocationId = dto.PickupLocationId,
                    DropoffLocationId = dto.DropoffLocationId,
                    PickupAddress = pickupResult.Value?.Name,
                    DropoffAddress = dropoffResult.Value?.Name,
                    HasTraveled = dto.HasTraveled,
                    Price = reservePrice.Price,
                    Status = PassengerStatusEnum.Confirmed,
                    CustomerId = payer.CustomerId,
                    DocumentNumber = payer.DocumentNumber,
                    FirstName = payer.FirstName,
                    LastName = payer.LastName,
                    Phone = $"{payer.Phone1} / {payer.Phone2}",
                    Email = payer.Email
                };

                reserve.Passengers.Add(passenger);

                _context.Passengers.Add(passenger);

                totalExpectedAmount += reservePrice.Price;
            }

            await _context.SaveChangesWithOutboxAsync();

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

            // Identificar reservas seleccionadas por el admin (pueden ser 1=Ida o 2=Ida/Vuelta)
            var distinctReserveIds = passengerReserves.Items
                .Select(i => i.ReserveId)
                .Distinct()
                .ToList();

            // Ordenar las seleccionadas: la primera es la IDA (más próxima de las seleccionadas)
            var orderedSelectedReserveIds = distinctReserveIds
                .OrderBy(id => reserveMap.TryGetValue(id, out var r) ? r.ReserveDate : DateTime.MaxValue)
                .ThenBy(id => reserveMap.TryGetValue(id, out var r) ? r.DepartureHour : TimeSpan.MaxValue)
                .ThenBy(id => id)
                .ToList();

            var idaReserveId = orderedSelectedReserveIds.First();
            var idaReserve = reserveMap[idaReserveId];

            // BUSCAR la reserva MÁS PRÓXIMA a salir (independiente del cliente)
            // Esto representa la "caja actual" donde debe quedar el pago
            var now = DateTime.UtcNow;
            var closestReserveInDb = await _context.Reserves
                .OrderBy(r => r.ReserveDate)
                .ThenBy(r => r.DepartureHour)
                .ThenBy(r => r.ReserveId)
                .FirstOrDefaultAsync();

            // Determinar dónde va el pago y el estado
            int parentReserveId;
            List<int> childReserveIds;
            StatusPaymentEnum paymentStatus;

            // Comparar: ¿La IDA seleccionada es la más próxima a salir?
            bool idaIsClosest = closestReserveInDb == null || closestReserveInDb.ReserveId == idaReserveId;

            if (idaIsClosest)
            {
                // La IDA ES la más próxima → Pago va a la IDA, estado = Paid
                parentReserveId = idaReserveId;
                childReserveIds = orderedSelectedReserveIds.Skip(1).ToList(); // Vuelta si existe
                paymentStatus = StatusPaymentEnum.Paid;
            }
            else
            {
                // Hay otra reserva más próxima → Pago va a esa, estado = PrePayment
                parentReserveId = closestReserveInDb.ReserveId;
                childReserveIds = orderedSelectedReserveIds.ToList(); // TODAS las seleccionadas son hijas
                paymentStatus = StatusPaymentEnum.PrePayment;

                // Agregar la más próxima al mapa si no está
                if (!reserveMap.ContainsKey(parentReserveId))
                {
                    reserveMap[parentReserveId] = closestReserveInDb;
                }
            }

            // Total efectivamente abonado (uno o varios medios)
            var primaryMethod = (PaymentMethodEnum)passengerReserves.Payments.First().PaymentMethod;

            // c) PAGO PADRE (reserva más reciente por orden cronológico)
            var parentPayment = new ReservePayment
            {
                ReserveId = parentReserveId,
                CustomerId = payer.CustomerId,
                PayerDocumentNumber = payer.DocumentNumber,
                PayerName = $"{payer.FirstName} {payer.LastName}",
                PayerEmail = payer.Email,
                Amount = totalProvidedAmount,             // total consolidado
                Method = primaryMethod,                   // método "principal" informativo
                Status = paymentStatus,
                StatusDetail = paymentStatus == StatusPaymentEnum.PrePayment
                    ? "paid_in_advance"
                    : "paid_on_departure"
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
                        Status = paymentStatus,
                        ParentReservePaymentId = parentPayment.ReservePaymentId
                    };
                    _context.ReservePayments.Add(breakdownChild);
                }
            }

            // e) "Link children" (monto 0) para cada tramo adicional (vuelta, etc.)
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
                    Status = paymentStatus,
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
        return await _unitOfWork.ExecuteInTransactionAsync<CreateReserveExternalResult>(async () =>
        {
            return await CreatePassengerReservesExternalCore(dto);
        });
    }

    private async Task<Result<CreateReserveExternalResult>> CreatePassengerReservesExternalCore(
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

            if (reserve.Passengers.Any(p => p.DocumentNumber == passengerDto.DocumentNumber))
                return Result.Failure<CreateReserveExternalResult>(
                    ReserveError.PassengerAlreadyExists(passengerDto.DocumentNumber));

            var pickupResult = await GetDirectionAsync(passengerDto.PickupLocationId, "Pickup");
            if (pickupResult.IsFailure)
                return Result.Failure<CreateReserveExternalResult>(pickupResult.Error);

            var dropoffResult = await GetDirectionAsync(passengerDto.DropoffLocationId, "Dropoff");
            if (dropoffResult.IsFailure)
                return Result.Failure<CreateReserveExternalResult>(dropoffResult.Error);

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
            _context.Passengers.Add(newPassenger);

            totalExpectedAmount += reservePrice.Price;

            if (!reserves.Any(p => p.ReserveId == reserve.ReserveId))
            {
                reserves.Add(reserve);
            }
        }

        await _context.SaveChangesWithOutboxAsync();

        if (dto.Payment is null)
        {
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

            var resultPayment = await CreatePayment(dto.Payment, reserves);
            if (resultPayment.IsFailure)
                return Result.Failure<CreateReserveExternalResult>(resultPayment.Error);

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(new CreateReserveExternalResult(PaymentStatus.Approved, null));
        }
    }

    private async Task<Result<bool>> CreatePayment(CreatePaymentExternalRequestDto paymentData, List<Reserve> reserves)
    {
        var orderedReserves = reserves
            .OrderBy(r => r.ReserveDate)
            .ThenBy(r => r.ReserveId)
            .ToList();

        var mainReserve = orderedReserves.First();

        var payingCustomer = await _context.Customers
            .SingleOrDefaultAsync(c => c.DocumentNumber == paymentData.IdentificationNumber);

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
            _context.Passengers.Update(p);
        }

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }


    private async Task<Result<int>> CreatePendingPayment(
      decimal amount,
      List<Reserve> reserves,
      PassengerReserveExternalCreateRequestDto firstPassenger)
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

    private Result ValidateUserReserveCombination(List<PassengerReserveExternalCreateRequestDto> items)
    {
        if (items == null || items.Count == 0)
            return Result.Failure(ReserveError.InvalidReserveCombination("No hay ítems para validar."));

        // 1) Agrupar por reserva y obtener los tipos distintos por cada reserva
        var byReserve = items
            .GroupBy(i => i.ReserveId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(i => (ReserveTypeIdEnum)i.ReserveTypeId).Distinct().ToList()
            );

        // 2) Dentro de una misma reserva no puede haber más de un tipo
        var mixedTypeReserveIds = byReserve
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => kv.Key)
            .ToList();

        if (mixedTypeReserveIds.Any())
            return Result.Failure(ReserveError.InvalidReserveCombination(
                $"La(s) reserva(s) {string.Join(", ", mixedTypeReserveIds)} tiene(n) más de un tipo asignado."));

        // 3) Máximo 2 reservas
        var distinctReserveIds = byReserve.Keys.ToList();
        if (distinctReserveIds.Count > 2)
            return Result.Failure(ReserveError.InvalidReserveCombination(
                "Solo se permite reservar hasta 2 reservas: ida y vuelta."));

        // 4) Tomar el tipo (único) de cada reserva
        var typesPerReserve = byReserve.ToDictionary(kv => kv.Key, kv => kv.Value.Single());

        if (distinctReserveIds.Count == 1)
        {
            var singleType = typesPerReserve.Values.Single();
            if (singleType == ReserveTypeIdEnum.IdaVuelta)
                return Result.Failure(ReserveError.InvalidReserveCombination(
                    "No se puede reservar únicamente la vuelta sin haber reservado ida."));
            return Result.Success();
        }

        // 5) Si hay 2 reservas, la combinación válida es exactamente Ida + IdaVuelta
        var typeSet = new HashSet<ReserveTypeIdEnum>(typesPerReserve.Values);
        if (typeSet.SetEquals(new[] { ReserveTypeIdEnum.Ida, ReserveTypeIdEnum.IdaVuelta }))
            return Result.Success();

        return Result.Failure(ReserveError.InvalidReserveCombination(
            "La combinación válida es exactamente Ida + IdaVuelta."));
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

        if (mpPayment.Status.Equals("in_process") || mpPayment.Status.Equals("pending"))
        {
            return true;
        }

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
                             p.Customer != null ? p.Customer.CurrentBalance : 0,
                             rp.Service.Vehicle.AvailableQuantity - rp.Passengers.Count,
                             null,
                             0,
                             false,
                             false))
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
            .Select(rp =>
            {
                int totalReserved = rp.Passengers
                    .Count(p => p.Status == PassengerStatusEnum.Confirmed
                             || p.Status == PassengerStatusEnum.PendingPayment);
                var arrivalTime = rp.DepartureHour.Add(rp.Service.EstimatedDuration);
                return new ReserveExternalReportResponseDto(
                    rp.ReserveId,
                    rp.Service.Origin.Name,
                    rp.Service.Destination.Name,
                    rp.DepartureHour.ToString(@"hh\:mm"),
                    rp.ReserveDate,
                    rp.Service.EstimatedDuration.ToString(@"hh\:mm"),
                    arrivalTime.ToString(@"hh\:mm"),
                    rp.Service.ReservePrices.FirstOrDefault(p => p.ReserveTypeId == ReserveTypeIdEnum.Ida)?.Price ?? 0,
                    rp.Service.Vehicle.AvailableQuantity - totalReserved,
                    rp.Service.Vehicle.InternalNumber
                );
            })
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
                .Select(rp =>
                {
                    int totalReserved = rp.Passengers
                        .Count(p => p.Status == PassengerStatusEnum.Confirmed
                                 || p.Status == PassengerStatusEnum.PendingPayment);
                    var arrivalTime = rp.DepartureHour.Add(rp.Service.EstimatedDuration);
                    return new ReserveExternalReportResponseDto(
                        rp.ReserveId,
                        rp.Service.Origin.Name,
                        rp.Service.Destination.Name,
                        rp.DepartureHour.ToString(@"hh\:mm"),
                        rp.ReserveDate,
                        rp.Service.EstimatedDuration.ToString(@"hh\:mm"),
                        arrivalTime.ToString(@"hh\:mm"),
                        rp.Service.ReservePrices.FirstOrDefault(p => p.ReserveTypeId == ReserveTypeIdEnum.Ida)?.Price ?? 0,
                        rp.Service.Vehicle.AvailableQuantity - totalReserved,
                        rp.Service.Vehicle.InternalNumber
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
                .ThenInclude(r => r.Service)
                    .ThenInclude(s => s.Vehicle)
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

        // Obtener pagos de la reserva para enriquecer la respuesta
        var reservePayments = await _context.ReservePayments
            .AsNoTracking()
            .Where(rp => rp.ReserveId == reserveId)
            .ToListAsync();

        // Obtener IDs de pagos padres que están en otras reservas (para link children)
        var parentPaymentIds = reservePayments
            .Where(p => p.ParentReservePaymentId.HasValue)
            .Select(p => p.ParentReservePaymentId!.Value)
            .Distinct()
            .ToList();

        // Obtener los pagos padres que están en otras reservas
        var parentPaymentsFromOtherReserves = parentPaymentIds.Any()
            ? await _context.ReservePayments
                .AsNoTracking()
                .Where(rp => parentPaymentIds.Contains(rp.ReservePaymentId))
                .ToListAsync()
            : new List<ReservePayment>();

        // También obtener los hijos de desglose de esos padres (pueden estar en la reserva padre)
        var parentIdsFromOther = parentPaymentsFromOtherReserves.Select(p => p.ReservePaymentId).ToList();
        var breakdownChildrenFromOtherReserves = parentIdsFromOther.Any()
            ? await _context.ReservePayments
                .AsNoTracking()
                .Where(rp => rp.ParentReservePaymentId.HasValue
                    && parentIdsFromOther.Contains(rp.ParentReservePaymentId.Value)
                    && rp.Amount > 0)
                .ToListAsync()
            : new List<ReservePayment>();

        // Agrupar pagos por CustomerId
        var paymentsByCustomer = new Dictionary<int, (string Methods, decimal Amount)>();

        // Procesar pagos directos en esta reserva (padres locales)
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
                .ToList();

            var totalAmount = parentPayments.Sum(p => p.Amount);

            paymentsByCustomer[group.Key] = (string.Join(", ", methods), totalAmount);
        }

        // Procesar link children (pagos que están en otra reserva - caja actual)
        var linkChildren = reservePayments
            .Where(p => p.ParentReservePaymentId.HasValue && p.Amount == 0 && p.CustomerId.HasValue)
            .ToList();

        foreach (var linkChild in linkChildren)
        {
            var customerId = linkChild.CustomerId!.Value;

            // Si ya procesamos este customer con pagos directos, saltar
            if (paymentsByCustomer.ContainsKey(customerId))
                continue;

            // Buscar el pago padre en otra reserva
            var parentPayment = parentPaymentsFromOtherReserves
                .FirstOrDefault(p => p.ReservePaymentId == linkChild.ParentReservePaymentId);

            if (parentPayment == null)
                continue;

            // Buscar hijos de desglose del padre
            var breakdownChildren = breakdownChildrenFromOtherReserves
                .Where(p => p.ParentReservePaymentId == parentPayment.ReservePaymentId)
                .ToList();

            var paymentsForMethods = breakdownChildren.Any()
                ? breakdownChildren
                : new List<ReservePayment> { parentPayment };

            var methods = paymentsForMethods
                .Select(p => GetPaymentMethodName(p.Method))
                .Distinct()
                .ToList();

            paymentsByCustomer[customerId] = (string.Join(", ", methods), parentPayment.Amount);
        }

        var sortMappings = new Dictionary<string, Expression<Func<Passenger, object>>>
        {
            ["passengerfullname"] = p => p.FirstName + " " + p.LastName,
            ["documentnumber"] = p => p.DocumentNumber,
            ["email"] = p => p.Email
        };

        // Obtener pasajeros paginados
        var passengers = await query.ToListAsync();

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(requestDto.SortBy) && sortMappings.ContainsKey(requestDto.SortBy.ToLower()))
        {
            var sortKey = sortMappings[requestDto.SortBy.ToLower()].Compile();
            passengers = requestDto.SortDescending
                ? passengers.OrderByDescending(sortKey).ToList()
                : passengers.OrderBy(sortKey).ToList();
        }

        // Mapear a DTOs con información de pagos
        var allItems = passengers.Select(p =>
        {
            var paymentInfo = p.CustomerId.HasValue && paymentsByCustomer.ContainsKey(p.CustomerId.Value)
                ? paymentsByCustomer[p.CustomerId.Value]
                : (Methods: (string?)null, Amount: 0m);

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
                p.Reserve.Service.Vehicle.AvailableQuantity - p.Reserve.Passengers.Count,
                paymentInfo.Methods,
                paymentInfo.Amount,
                paymentInfo.Amount > 0,
                p.HasTraveled);
        }).ToList();

        var pagedResult = PagedReportResponseDto<PassengerReserveReportResponseDto>.Create(
            allItems,
            requestDto.PageNumber,
            requestDto.PageSize);

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

            Customer payer = await _context.Customers.FindAsync(customerId);
            if (payer == null)
                return Result.Failure<bool>(CustomerError.NotFound);

            // BUSCAR la reserva MÁS PRÓXIMA a salir (caja actual)
            var closestReserveInDb = await _context.Reserves
                .OrderBy(r => r.ReserveDate)
                .ThenBy(r => r.DepartureHour)
                .ThenBy(r => r.ReserveId)
                .FirstOrDefaultAsync();

            // Determinar dónde va el pago y el estado
            int parentReserveId;
            StatusPaymentEnum paymentStatus;
            bool needsLinkChild;

            // ¿La reserva indicada es la más próxima a salir?
            bool reserveIsClosest = closestReserveInDb == null || closestReserveInDb.ReserveId == reserveId;

            if (reserveIsClosest)
            {
                // La reserva indicada ES la más próxima → Pago va a ella, estado = Paid
                parentReserveId = reserveId;
                paymentStatus = StatusPaymentEnum.Paid;
                needsLinkChild = false;
            }
            else
            {
                // Hay otra reserva más próxima → Pago va a esa (caja actual), estado = PrePayment
                parentReserveId = closestReserveInDb.ReserveId;
                paymentStatus = StatusPaymentEnum.PrePayment;
                needsLinkChild = true;
            }

            var primaryMethod = (PaymentMethodEnum)payments.First().PaymentMethod;

            // Crear pago PADRE en la reserva más próxima (caja actual)
            var parentPayment = new ReservePayment
            {
                ReserveId = parentReserveId,
                CustomerId = payer.CustomerId,
                PayerDocumentNumber = payer.DocumentNumber,
                PayerName = $"{payer.FirstName} {payer.LastName}",
                PayerEmail = payer.Email,
                Amount = providedAmount,
                Method = primaryMethod,
                Status = paymentStatus,
                StatusDetail = paymentStatus == StatusPaymentEnum.PrePayment
                    ? "paid_in_advance"
                    : "paid_on_departure"
            };
            _context.ReservePayments.Add(parentPayment);
            await _context.SaveChangesWithOutboxAsync(); // necesitamos el Id del padre

            // Si hay split de medios (>=2), crear hijos de desglose SOLO en la reserva padre
            if (payments.Count > 1)
            {
                foreach (var payment in payments)
                {
                    var breakdownChild = new ReservePayment
                    {
                        ReserveId = parentReserveId,
                        CustomerId = payer.CustomerId,
                        PayerDocumentNumber = payer.DocumentNumber,
                        PayerName = $"{payer.FirstName} {payer.LastName}",
                        PayerEmail = payer.Email,
                        Amount = payment.TransactionAmount,
                        Method = (PaymentMethodEnum)payment.PaymentMethod,
                        Status = paymentStatus,
                        ParentReservePaymentId = parentPayment.ReservePaymentId
                    };
                    _context.ReservePayments.Add(breakdownChild);
                }
            }

            // Si el pago fue a otra reserva (caja actual), crear "link child" en la reserva original
            if (needsLinkChild)
            {
                var linkChild = new ReservePayment
                {
                    ReserveId = reserveId,
                    CustomerId = payer.CustomerId,
                    PayerDocumentNumber = payer.DocumentNumber,
                    PayerName = $"{payer.FirstName} {payer.LastName}",
                    PayerEmail = payer.Email,
                    Amount = 0m,
                    Method = primaryMethod,
                    Status = paymentStatus,
                    ParentReservePaymentId = parentPayment.ReservePaymentId
                };
                _context.ReservePayments.Add(linkChild);
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

            // Asiento de Payment (negativo) e impacto en saldo
            var transaction = new CustomerAccountTransaction
            {
                CustomerId = payer.CustomerId,
                Date = DateTime.UtcNow,
                Type = TransactionType.Payment,
                Amount = -providedAmount,
                Description = $"Pago aplicado a reserva #{reserveId}",
                RelatedReserveId = parentReserveId,
                ReservePaymentId = parentPayment.ReservePaymentId
            };
            _context.CustomerAccountTransactions.Add(transaction);

            payer.CurrentBalance -= providedAmount;
            _context.Customers.Update(payer);

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    public async Task<Result<LockReserveSlotsResponseDto>> LockReserveSlots(LockReserveSlotsRequestDto request)
    {
        const int maxRetries = 3;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var userEmail = _userContext.Email;
                    var userId = _userContext.UserId;

                    Customer? associatedCustomer = null;
                    if (userId != null && userId > 0)
                    {
                        var user = await _context.Users
                            .Include(u => u.Customer)
                            .FirstOrDefaultAsync(u => u.UserId == userId);
                        associatedCustomer = user?.Customer;
                    }

                    var activeLocksCount = await _context.ReserveSlotLocks
                        .CountAsync(l => l.UserEmail == userEmail &&
                                       l.Status == ReserveSlotLockStatus.Active &&
                                       l.ExpiresAt > DateTime.UtcNow);

                    if (activeLocksCount >= _reserveOptions.MaxSimultaneousLocksPerUser)
                        return Result.Failure<LockReserveSlotsResponseDto>(ReserveSlotLockError.MaxSimultaneousLocksExceeded);

                    var availableSlots = await GetAvailableSlotsWithOptimisticLocking(request.OutboundReserveId, request.ReturnReserveId);

                    if (availableSlots < request.PassengerCount)
                        return Result.Failure<LockReserveSlotsResponseDto>(ReserveSlotLockError.InsufficientSlots);

                    var lockToken = Guid.NewGuid().ToString();
                    var expiresAt = DateTime.UtcNow.AddMinutes(_reserveOptions.SlotLockTimeoutMinutes);

                    var slotLock = new ReserveSlotLock
                    {
                        LockToken = lockToken,
                        OutboundReserveId = request.OutboundReserveId,
                        ReturnReserveId = request.ReturnReserveId,
                        SlotsLocked = request.PassengerCount,
                        ExpiresAt = expiresAt,
                        Status = ReserveSlotLockStatus.Active,
                        UserEmail = userEmail,
                        UserDocumentNumber = associatedCustomer?.DocumentNumber,
                        CustomerId = associatedCustomer?.CustomerId,
                        RowVersion = new byte[8]
                    };

                    _context.ReserveSlotLocks.Add(slotLock);
                    await _context.SaveChangesWithOutboxAsync();

                    return Result.Success(new LockReserveSlotsResponseDto(
                        lockToken,
                        expiresAt,
                        _reserveOptions.SlotLockTimeoutMinutes
                    ));
                });
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(Random.Shared.Next(10, 100)); // Jitter to avoid thundering herd
                continue;
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("deadlock") == true && attempt < maxRetries - 1)
            {
                await Task.Delay(Random.Shared.Next(50, 200));
                continue;
            }
        }

        return Result.Failure<LockReserveSlotsResponseDto>(
            Error.Failure("ConcurrencyConflict", "Unable to acquire slot lock due to high concurrency. Please try again."));
    }

    private async Task<int> GetAvailableSlotsWithOptimisticLocking(int outboundReserveId, int? returnReserveId = null)
    {
        var reserveIds = new List<int> { outboundReserveId };
        if (returnReserveId.HasValue) reserveIds.Add(returnReserveId.Value);

        var reserves = await _context.Reserves
            .Include(r => r.Passengers)
            .Include(r => r.Service.Vehicle)
            .Where(r => reserveIds.Contains(r.ReserveId))
            .ToListAsync();

        var minAvailable = int.MaxValue;

        foreach (var reserve in reserves)
        {
            var confirmedPassengers = reserve.Passengers
                .Count(p => p.Status == PassengerStatusEnum.Confirmed ||
                           p.Status == PassengerStatusEnum.PendingPayment);

            var activeLocks = await _context.ReserveSlotLocks
                .Where(l => (l.OutboundReserveId == reserve.ReserveId || l.ReturnReserveId == reserve.ReserveId) &&
                           l.Status == ReserveSlotLockStatus.Active &&
                           l.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            var totalActiveLocks = activeLocks.Sum(l => l.SlotsLocked);

            var available = reserve.Service.Vehicle.AvailableQuantity - confirmedPassengers - totalActiveLocks;
            minAvailable = Math.Min(minAvailable, available);

            reserve.UpdatedDate = DateTime.UtcNow;
            reserve.UpdatedBy = "SlotLockSystem";
        }

        return Math.Max(0, minAvailable);
    }

    public async Task<Result<CreateReserveExternalResult>> CreatePassengerReservesWithLock(CreateReserveWithLockRequestDto request)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var slotLock = await _context.ReserveSlotLocks
                .FirstOrDefaultAsync(l => l.LockToken == request.LockToken &&
                                         l.Status == ReserveSlotLockStatus.Active &&
                                         l.ExpiresAt > DateTime.UtcNow);

            if (slotLock == null)
                return Result.Failure<CreateReserveExternalResult>(ReserveSlotLockError.InvalidOrExpiredLock);

            var requestReserveIds = request.Items.Select(i => i.ReserveId).Distinct().OrderBy(x => x).ToList();
            var lockReserveIds = new List<int> { slotLock.OutboundReserveId };
            if (slotLock.ReturnReserveId.HasValue)
                lockReserveIds.Add(slotLock.ReturnReserveId.Value);
            lockReserveIds = lockReserveIds.OrderBy(x => x).ToList();

            if (!requestReserveIds.SequenceEqual(lockReserveIds))
                return Result.Failure<CreateReserveExternalResult>(ReserveSlotLockError.LockReserveMismatch);

            if (request.Items.Count != slotLock.SlotsLocked)
                return Result.Failure<CreateReserveExternalResult>(ReserveSlotLockError.LockReserveMismatch);

            var externalDto = new PassengerReserveCreateRequestWrapperExternalDto(
                request.Payment,
                request.Items
            );

            var result = await CreatePassengerReservesExternalCore(externalDto);

            if (result.IsSuccess)
            {
                slotLock.Status = ReserveSlotLockStatus.Used;
                slotLock.UpdatedDate = DateTime.UtcNow;
                _context.ReserveSlotLocks.Update(slotLock);
                await _context.SaveChangesWithOutboxAsync();
            }

            return result;
        });
    }

    public async Task<Result<bool>> CancelReserveSlotLock(string lockToken)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var slotLock = await _context.ReserveSlotLocks
                .FirstOrDefaultAsync(l => l.LockToken == lockToken &&
                                         l.Status == ReserveSlotLockStatus.Active);

            if (slotLock == null)
                return Result.Failure<bool>(ReserveSlotLockError.LockNotFound);

            slotLock.Status = ReserveSlotLockStatus.Cancelled;
            slotLock.UpdatedDate = DateTime.UtcNow;

            _context.ReserveSlotLocks.Update(slotLock);
            await _context.SaveChangesWithOutboxAsync();

            return Result.Success(true);
        });
    }

    public async Task<Result<bool>> CleanupExpiredReserveSlotLocks()
    {
        var expiredLocks = await _context.ReserveSlotLocks
            .Where(l => l.Status == ReserveSlotLockStatus.Active &&
                       l.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        if (expiredLocks.Any())
        {
            foreach (var expiredLock in expiredLocks)
            {
                expiredLock.Status = ReserveSlotLockStatus.Expired;
                expiredLock.UpdatedDate = DateTime.UtcNow;
            }

            _context.ReserveSlotLocks.UpdateRange(expiredLocks);
            await _context.SaveChangesWithOutboxAsync();
        }

        return Result.Success(true);
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

        // Obtener pagos de la reserva (solo padres o pagos sin hijos para evitar duplicados)
        // Si hay hijos de desglose (breakdown), usamos esos para el resumen por método
        // Si no hay hijos, usamos el pago padre directamente
        var payments = await _context.ReservePayments
            .AsNoTracking()
            .Where(p => p.ReserveId == reserveId)
            .ToListAsync();

        // Separar pagos padres de hijos
        var parentPayments = payments.Where(p => p.ParentReservePaymentId == null).ToList();
        var childBreakdownPayments = payments.Where(p => p.ParentReservePaymentId != null && p.Amount > 0).ToList();

        // Si hay hijos de desglose (Amount > 0), usamos esos para el detalle por método
        // Si no hay, usamos los padres
        var paymentsForSummary = childBreakdownPayments.Any() ? childBreakdownPayments : parentPayments;

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
            _ => method.ToString()
        };
    }
}