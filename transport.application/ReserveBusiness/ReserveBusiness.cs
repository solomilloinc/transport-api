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
using Transport.Domain.CashBoxes;
using Transport.Domain.CashBoxes.Abstraction;
using Transport.Domain.Cities;
using Transport.Domain.Customers;
using Transport.Domain.Customers.Abstraction;
using Transport.Domain.Directions;
using Transport.Domain.Drivers;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Reserves.Abstraction;
using Transport.Domain.Services;
using Transport.Domain.Trips;
using Transport.Domain.Users;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Customer;
using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Payment;
using Transport.SharedKernel.Contracts.Reserve;
using Transport.SharedKernel.Contracts.Service;
using Transport.SharedKernel.Contracts.Trip;
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
    private readonly ICashBoxBusiness _cashBoxBusiness;

    public ReserveBusiness(IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IMercadoPagoPaymentGateway paymentGateway,
        ICustomerBusiness customerBusiness,
        IReserveOption reserveOptions,
        ICashBoxBusiness cashBoxBusiness)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _paymentGateway = paymentGateway;
        _customerBusiness = customerBusiness;
        _reserveOptions = reserveOptions;
        _cashBoxBusiness = cashBoxBusiness;
    }

    public async Task<Result<int>> CreateReserve(ReserveCreateDto dto)
    {
        // Validate Vehicle
        var vehicle = await _context.Vehicles.FindAsync(dto.VehicleId);
        if (vehicle is null)
            return Result.Failure<int>(VehicleError.VehicleNotFound);

        if (vehicle.Status != EntityStatusEnum.Active)
            return Result.Failure<int>(VehicleError.VehicleNotAvailable);

        // Validate Trip
        var trip = await _context.Trips
            .Include(t => t.OriginCity)
            .Include(t => t.DestinationCity)
            .FirstOrDefaultAsync(t => t.TripId == dto.TripId);

        if (trip is null)
            return Result.Failure<int>(TripError.TripNotFound);
        
        if (trip.Status != EntityStatusEnum.Active)
             return Result.Failure<int>(TripError.TripNotActive);

        // Validate Driver if provided
        if (dto.DriverId.HasValue)
        {
            var driver = await _context.Drivers.FindAsync(dto.DriverId.Value);
            if (driver is null)
                return Result.Failure<int>(DriverError.DriverNotFound);
        }

        var reserve = new Reserve
        {
            ReserveDate = dto.ReserveDate,
            VehicleId = dto.VehicleId,
            DriverId = dto.DriverId,
            TripId = trip.TripId,
            OriginName = trip.OriginCity.Name,
            DestinationName = trip.DestinationCity.Name,
            ServiceName = $"{trip.OriginCity.Name} - {trip.DestinationCity.Name}",
            DepartureHour = dto.DepartureHour,
            EstimatedDuration = dto.EstimatedDuration,
            IsHoliday = dto.IsHoliday,
            Status = ReserveStatusEnum.Confirmed,
            ServiceId = null,
            ServiceScheduleId = null
        };

        // Add allowed directions whitelist for individual reserve
        if (dto.AllowedDirectionIds?.Any() == true)
        {
            foreach (var directionId in dto.AllowedDirectionIds.Distinct())
            {
                reserve.AllowedDirections.Add(new ReserveDirection
                {
                    DirectionId = directionId
                });
            }
        }

        _context.Reserves.Add(reserve);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(reserve.ReserveId);
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
            var mainReserveId = passengerReserves.Items.Min(i => i.ReserveId);
            var servicesCache = new Dictionary<int, Service>();
            var reserveMap = new Dictionary<int, Reserve>();

            // Inferir ReserveRelatedId automáticamente si hay exactamente 2 reservas distintas (ida/vuelta)
            var reserveRelatedMap = new Dictionary<int, int?>();
            var distinctReserveIds = passengerReserves.Items.Select(i => i.ReserveId).Distinct().ToList();
            if (distinctReserveIds.Count == 2)
            {
                // El primero apunta al segundo y viceversa
                reserveRelatedMap[distinctReserveIds[0]] = distinctReserveIds[1];
                reserveRelatedMap[distinctReserveIds[1]] = distinctReserveIds[0];
            }

            decimal totalExpectedAmount = passengerReserves.Items.First().Price;

            // 2) Alta de pasajeros + validaciones
            foreach (var dto in passengerReserves.Items)
            {
                var reserve = await _context.Reserves
                    .Include(r => r.Passengers)
                    .Include(r => r.Driver)
                    .Include(r => r.Trip)
                    .SingleOrDefaultAsync(r => r.ReserveId == dto.ReserveId);

                if (reserve is null)
                    return Result.Failure<bool>(ReserveError.NotFound);

                if (reserve.Status != ReserveStatusEnum.Confirmed)
                    return Result.Failure<bool>(ReserveError.NotAvailable);

                reserveMap[reserve.ReserveId] = reserve;

                // Only lookup service if the reserve has one
                if (reserve.ServiceId.HasValue)
                {
                    if (!servicesCache.TryGetValue(reserve.ServiceId.Value, out var service))
                    {
                        service = await _context.Services
                            .Include(s => s.Trip.OriginCity)
                            .Include(s => s.Trip.DestinationCity)
                            .SingleOrDefaultAsync(s => s.ServiceId == reserve.ServiceId.Value);

                        if (service is null)
                            return Result.Failure<bool>(ServiceError.ServiceNotFound);

                        servicesCache[reserve.ServiceId.Value] = service;
                    }
                }

                var vehicle = await _context.Vehicles.FindAsync(reserve.VehicleId);
                var existingPassengerCount = reserve.Passengers.Count;
                var totalAfterInsert = existingPassengerCount + passengerReserves.Items.Count;

                if (vehicle == null || totalAfterInsert > vehicle.AvailableQuantity)
                    return Result.Failure<bool>(ReserveError.VehicleQuantityNotAvailable(
                        existingPassengerCount, passengerReserves.Items.Count, vehicle?.AvailableQuantity ?? 0));

                var reservePrice = await GetPassengerPriceAsync(
                    reserve.Trip.OriginCityId, reserve.Trip.DestinationCityId, (ReserveTypeIdEnum)dto.ReserveTypeId, dto.DropoffLocationId);
                
                if (reservePrice is null || dto.Price != reservePrice.Value)
                    return Result.Failure<bool>(ReserveError.PriceNotAvailable);

                var pickupResult = await GetDirectionAsync(dto.PickupLocationId, "Pickup");
                if (pickupResult.IsFailure) return Result.Failure<bool>(pickupResult.Error);

                var dropoffResult = await GetDirectionAsync(dto.DropoffLocationId, "Dropoff");
                if (dropoffResult.IsFailure) return Result.Failure<bool>(dropoffResult.Error);
                // Usar ReserveRelatedId del DTO si viene, sino inferir del mapa
                var inferredRelatedId = reserveRelatedMap.TryGetValue(dto.ReserveId, out var relatedId)
                    ? relatedId
                    : dto.ReserveRelatedId;

                var passenger = new Passenger
                {
                    ReserveId = reserve.ReserveId,
                    ReserveRelatedId = inferredRelatedId,
                    PickupLocationId = dto.PickupLocationId,
                    DropoffLocationId = dto.DropoffLocationId,
                    PickupAddress = pickupResult.Value?.Name,
                    DropoffAddress = dropoffResult.Value?.Name,
                    HasTraveled = dto.HasTraveled,
                    Price = reservePrice.Value,
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

            // Obtener la caja abierta
            var cashBoxResult = await _cashBoxBusiness.GetOpenCashBoxEntity();
            if (cashBoxResult.IsFailure)
                return Result.Failure<bool>(cashBoxResult.Error);

            var cashBox = cashBoxResult.Value;

            // Total efectivamente abonado (uno o varios medios)
            var primaryMethod = (PaymentMethodEnum)passengerReserves.Payments.First().PaymentMethod;

            // PAGO PADRE - siempre va a la reserva principal (IDA)
            var parentPayment = new ReservePayment
            {
                ReserveId = mainReserveId,
                CustomerId = payer.CustomerId,
                PayerDocumentNumber = payer.DocumentNumber,
                PayerName = $"{payer.FirstName} {payer.LastName}",
                PayerEmail = payer.Email,
                Amount = totalProvidedAmount,
                Method = primaryMethod,
                Status = StatusPaymentEnum.Paid,
                StatusDetail = "paid_on_departure",
                CashBoxId = cashBox.CashBoxId
            };
            _context.ReservePayments.Add(parentPayment);
            await _context.SaveChangesWithOutboxAsync(); // necesitamos el Id del padre

            // Si hay split de medios (>=2), crear hijos de desglose (Breakdown)
            if (passengerReserves.Payments.Count > 1)
            {
                foreach (var p in passengerReserves.Payments)
                {
                    var breakdownChild = new ReservePayment
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
                        CashBoxId = cashBox.CashBoxId
                    };
                    _context.ReservePayments.Add(breakdownChild);
                }
            }

            // Asiento de Payment (negativo) e impacto en saldo
            _context.CustomerAccountTransactions.Add(new CustomerAccountTransaction
            {
                CustomerId = payer.CustomerId,
                Date = DateTime.UtcNow,
                Type = TransactionType.Payment,
                Amount = -totalProvidedAmount,
                Description = $"Pago aplicado a {description}",
                RelatedReserveId = mainReserveId,
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

        // Inferir ReserveRelatedId automáticamente si hay exactamente 2 reservas (ida/vuelta)
        var reserveRelatedMap = new Dictionary<int, int?>();
        var reserveIds = dto.Items.Select(i => i.ReserveId).Distinct().OrderBy(id => id).ToList();
        if (reserveIds.Count == 2)
        {
            reserveRelatedMap[reserveIds[0]] = reserveIds[1];
            reserveRelatedMap[reserveIds[1]] = reserveIds[0];
        }

        // Calcular monto esperado: precio * cantidad de pasajeros (contar solo items de ida)
        var idaReserveId = reserveIds.First();
        var passengerCount = dto.Items.Count(i => i.ReserveId == idaReserveId);
        decimal totalExpectedAmount = dto.Items.First().Price * passengerCount;

        foreach (var passengerDto in dto.Items)
        {
            var reserve = await _context.Reserves
               .Include(r => r.Passengers)
               .Include(r => r.Trip)
               .SingleOrDefaultAsync(r => r.ReserveId == passengerDto.ReserveId);

            if (reserve is null)
                return Result.Failure<CreateReserveExternalResult>(ReserveError.NotFound);

            if (reserve.Status != ReserveStatusEnum.Confirmed)
                return Result.Failure<CreateReserveExternalResult>(ReserveError.NotAvailable);

            var service = await _context.Services
                .Include(s => s.Trip.OriginCity)
                .Include(s => s.Trip.DestinationCity)
                .SingleOrDefaultAsync(s => s.ServiceId == reserve.ServiceId);

            var vehicle = await _context.Vehicles.FindAsync(reserve.VehicleId);

            var existingPassengerCount = reserve.Passengers.Count;
            var totalAfterInsert = existingPassengerCount + dto.Items.Count;

            if (totalAfterInsert > vehicle.AvailableQuantity)
                return Result.Failure<CreateReserveExternalResult>(
                    ReserveError.VehicleQuantityNotAvailable(existingPassengerCount, dto.Items.Count, vehicle.AvailableQuantity));

            var reservePrice = await GetPassengerPriceAsync(
                reserve.Trip.OriginCityId, reserve.Trip.DestinationCityId, (ReserveTypeIdEnum)passengerDto.ReserveTypeId, passengerDto.DropoffLocationId);

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

            // Usar ReserveRelatedId del DTO si viene, sino inferir del mapa
            var inferredRelatedId = reserveRelatedMap.TryGetValue(passengerDto.ReserveId, out var relatedId)
                ? relatedId
                : passengerDto.ReserveRelatedId;

            var newPassenger = new Passenger
            {
                ReserveId = reserve.ReserveId,
                ReserveRelatedId = inferredRelatedId,
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
                Price = reservePrice.Value,
                Status = dto.Payment is null ? PassengerStatusEnum.PendingPayment : PassengerStatusEnum.Confirmed,
                CustomerId = existingCustomer?.CustomerId,
            };

            reserve.Passengers.Add(newPassenger);
            _context.Passengers.Add(newPassenger);

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

            // Lanzar evento para enviar email de confirmacion
            var mainReserve = reserves.OrderBy(r => r.ReserveId).First();
            var firstPassenger = dto.Items.First();
            var reserveCreatedEvent = new CustomerReserveCreatedEvent(
                ReserveId: mainReserve.ReserveId,
                CustomerId: firstPassenger.CustomerId,
                CustomerEmail: firstPassenger.Email,
                CustomerFullName: $"{firstPassenger.FirstName} {firstPassenger.LastName}",
                ServiceName: mainReserve.ServiceName,
                OriginName: mainReserve.OriginName,
                DestinationName: mainReserve.DestinationName,
                ReserveDate: mainReserve.ReserveDate,
                DepartureHour: mainReserve.DepartureHour,
                TotalPrice: totalExpectedAmount
            );
            mainReserve.Raise(reserveCreatedEvent);

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

        // Obtener CashBox abierta (opcional para pagos online)
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
            CashBoxId = cashBoxId
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

        // 4) Estado de pasajeros según resultado
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

        // Obtener CashBox abierta (opcional para pagos online)
        var cashBoxResult = await _cashBoxBusiness.GetOpenCashBoxEntity();
        var cashBoxId = cashBoxResult.IsSuccess ? cashBoxResult.Value.CashBoxId : (int?)null;

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
            CashBoxId = cashBoxId
        };

        _context.ReservePayments.Add(parentPayment);
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
            var originName = reserve.ServiceId.HasValue && servicesCache.TryGetValue(reserve.ServiceId.Value, out var svc)
                ? svc.Trip.OriginCity.Name : reserve.OriginName;
            var destName = reserve.ServiceId.HasValue && servicesCache.TryGetValue(reserve.ServiceId.Value, out svc)
                ? svc.Trip.DestinationCity.Name : reserve.DestinationName;
            var type = passengerReserves.Items.First(i => i.ReserveId == rid).ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta
                ? "Ida y vuelta"
                : "Ida";
            return $"Reserva: {type} #{rid} - {originName} - {destName} {reserve.ReserveDate:HH:mm}";
        }

        var rid1 = reserveIds[0];
        var rid2 = reserveIds[1];
        var reserve1 = reserveMap[rid1];
        var reserve2 = reserveMap[rid2];

        var origin1 = reserve1.ServiceId.HasValue && servicesCache.TryGetValue(reserve1.ServiceId.Value, out var svc1)
            ? svc1.Trip.OriginCity.Name : reserve1.OriginName;
        var dest1 = reserve1.ServiceId.HasValue && servicesCache.TryGetValue(reserve1.ServiceId.Value, out svc1)
            ? svc1.Trip.DestinationCity.Name : reserve1.DestinationName;
        var origin2 = reserve2.ServiceId.HasValue && servicesCache.TryGetValue(reserve2.ServiceId.Value, out var svc2)
            ? svc2.Trip.OriginCity.Name : reserve2.OriginName;
        var dest2 = reserve2.ServiceId.HasValue && servicesCache.TryGetValue(reserve2.ServiceId.Value, out svc2)
            ? svc2.Trip.DestinationCity.Name : reserve2.DestinationName;

        var desc1 = $"Ida #{rid1} - {origin1} - {dest1} {reserve1.ReserveDate:HH:mm}";
        var desc2 = $"Vuelta #{rid2} - {dest2} - {origin2} {reserve2.ReserveDate:HH:mm}";

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
        var date = reserveDate.Date;

        // 1. Query base para contar y paginar
        var baseQuery = _context.Reserves
            .Include(r => r.Passengers)
            .Include(r => r.Vehicle)
            .Where(r => r.Status == ReserveStatusEnum.Confirmed)
            .Where(r => r.ReserveDate.Date == date);

        // 2. Contar total
        var totalCount = await baseQuery.CountAsync();

        // 3. Aplicar ordenamiento
        var sortBy = requestDto.SortBy?.ToLower() ?? "reservedate";
        var sortDesc = requestDto.SortDescending;

        IOrderedQueryable<Reserve> orderedQuery = sortBy switch
        {
            "serviceorigin" => sortDesc ? baseQuery.OrderByDescending(r => r.OriginName) : baseQuery.OrderBy(r => r.OriginName),
            "servicedest" => sortDesc ? baseQuery.OrderByDescending(r => r.DestinationName) : baseQuery.OrderBy(r => r.DestinationName),
            _ => sortDesc ? baseQuery.OrderByDescending(r => r.ReserveDate) : baseQuery.OrderBy(r => r.ReserveDate)
        };

        // 4. Aplicar paginación y obtener reservas
        var pageNumber = requestDto.PageNumber > 0 ? requestDto.PageNumber : 1;
        var pageSize = requestDto.PageSize > 0 ? requestDto.PageSize : 10;

        var reserves = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 5. Precios eliminados para optimización (se deben pedir por GetTripById bajo demanda)
        var tripIds = reserves.Select(r => r.TripId).Distinct().ToList();

        var tripDescriptions = await _context.Trips
            .Where(t => tripIds.Contains(t.TripId))
            .ToDictionaryAsync(t => t.TripId, t => t.Description);

        // 7. Proyectar a DTOs
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

        // Fetch the ida trip with prices
        var idaTrip = await _context.Trips
            .Where(t => t.TripId == tripId && t.Status == EntityStatusEnum.Active)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
            .FirstOrDefaultAsync();

        if (idaTrip == null)
            return Result.Failure<ReserveGroupedPagedReportResponseDto>(TripError.TripNotFound);

        var originId = idaTrip.OriginCityId;
        var destinationId = idaTrip.DestinationCityId;

        // Fetch the return trip (inverse route) if needed
        Trip? vueltaTrip = null;
        if (vueltaDate.HasValue)
        {
            vueltaTrip = await _context.Trips
                .Where(t => t.OriginCityId == destinationId && t.DestinationCityId == originId
                         && t.Status == EntityStatusEnum.Active)
                .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
                .FirstOrDefaultAsync();
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
                    rp.Vehicle.InternalNumber
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
                        rp.Vehicle.InternalNumber
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

        // Obtener pasajeros
        var passengers = await query.ToListAsync();

        // Obtener el count total de pasajeros de la reserva (sin filtros aplicados)
        var totalPassengersInReserve = await _context.Passengers
            .CountAsync(p => p.ReserveId == reserveId);

        // Obtener IDs de reservas relacionadas de estos pasajeros (ida/vuelta)
        var relatedReserveIds = passengers
            .Where(p => p.ReserveRelatedId.HasValue)
            .Select(p => p.ReserveRelatedId!.Value)
            .Distinct()
            .ToList();

        // Lista de todas las reservas de interés (la actual y sus relacionadas)
        var allRelevantReserveIds = new List<int> { reserveId };
        allRelevantReserveIds.AddRange(relatedReserveIds);

        // Obtener pagos de todas las reservas relevantes para enriquecer la respuesta
        var reservePayments = await _context.ReservePayments
            .AsNoTracking()
            .Where(rp => allRelevantReserveIds.Contains(rp.ReserveId))
            .ToListAsync();

        // Agrupar pagos por CustomerId
        var paymentsByCustomer = new Dictionary<int, (string Methods, decimal Amount)>();

        // Procesar pagos (padres) de todas las reservas involucradas
        var localParentPayments = reservePayments
            .Where(p => p.ParentReservePaymentId == null && p.CustomerId.HasValue)
            .GroupBy(p => p.CustomerId!.Value);

        foreach (var group in localParentPayments)
        {
            var parentPayments = group.ToList();
            // Breakdown children: ParentId != null && Amount > 0
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

        // Aplicar ordenamiento en memoria (ya que ya traemos la lista)

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(requestDto.SortBy) && sortMappings.ContainsKey(requestDto.SortBy.ToLower()))
        {
            var sortKey = sortMappings[requestDto.SortBy.ToLower()].Compile();
            passengers = requestDto.SortDescending
                ? passengers.OrderByDescending(sortKey).ToList()
                : passengers.OrderBy(sortKey).ToList();
        }

        // Pre-fetch prices for this reserve's route via Trip
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

        // Mapear a DTOs con información de pagos
        var allItems = passengers.Select(p =>
        {
            var paymentInfo = p.CustomerId.HasValue && paymentsByCustomer.ContainsKey(p.CustomerId.Value)
                ? paymentsByCustomer[p.CustomerId.Value]
                : (Methods: (string?)null, Amount: 0m);

            var paidAmount = paymentInfo.Amount;
            var isPayment = paymentInfo.Amount > 0;

            // Si no tiene pago, obtenemos el precio de la ruta
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

            // Obtener la caja abierta
            var cashBoxResult = await _cashBoxBusiness.GetOpenCashBoxEntity();
            if (cashBoxResult.IsFailure)
                return Result.Failure<bool>(cashBoxResult.Error);

            var cashBox = cashBoxResult.Value;
            var primaryMethod = (PaymentMethodEnum)payments.First().PaymentMethod;

            // Crear pago PADRE en la reserva indicada
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
            await _context.SaveChangesWithOutboxAsync(); // necesitamos el Id del padre

            // Si hay split de medios (>=2), crear hijos de desglose (Breakdown)
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
            .Include(r => r.Vehicle)
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

            var available = reserve.Vehicle.AvailableQuantity - confirmedPassengers - totalActiveLocks;
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

        // Obtener pagos de la reserva
        var payments = await _context.ReservePayments
            .AsNoTracking()
            .Where(p => p.ReserveId == reserveId)
            .ToListAsync();

        // Separar pagos padres de hijos (Breakdown: ParentId != null && Amount > 0)
        var parentPayments = payments.Where(p => p.ParentReservePaymentId == null).ToList();
        var childBreakdownPayments = payments
            .Where(p => p.ParentReservePaymentId != null && p.Amount > 0)
            .ToList();

        // Si hay hijos de desglose (Breakdown), usamos esos para el detalle por método
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
            PaymentMethodEnum.Transfer => "Transferencia",
            _ => method.ToString()
        };
    }

    private async Task<decimal?> GetPassengerPriceAsync(
        int originId, 
        int destinationId, 
        ReserveTypeIdEnum reserveTypeId, 
        int? dropoffLocationId)
    {
        // 1. Find the trip for this origin/destination
        var trip = await _context.Trips
            .Where(t => t.OriginCityId == originId
                     && t.DestinationCityId == destinationId
                     && t.Status == EntityStatusEnum.Active)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
            .FirstOrDefaultAsync();

        if (trip is null)
            return null;

        // 2. Filter prices for the specific reserve type (OneWay/RoundTrip)
        var relevantPrices = trip.Prices
            .Where(p => p.ReserveTypeId == reserveTypeId)
            .ToList();

        if (!relevantPrices.Any())
            return null;

        // 3. Determine the Dropoff City ID if a specific location is provided
        int? dropoffCityId = null;
        if (dropoffLocationId.HasValue)
        {
            var dropoffDirection = await _context.Directions.FindAsync(dropoffLocationId.Value);
            dropoffCityId = dropoffDirection?.CityId;
            
            // PRIORITY 1: Specific Price for this Direction (Stop)
            var directionPrice = relevantPrices.FirstOrDefault(p => p.DirectionId == dropoffLocationId.Value);
            if (directionPrice != null)
                return directionPrice.Price;
        }

        // PRIORITY 2: Price for the Dropoff City (intermediate city)
        if (dropoffCityId.HasValue)
        {
            var cityPrice = relevantPrices.FirstOrDefault(p => p.CityId == dropoffCityId.Value && p.DirectionId == null);
            if (cityPrice != null)
                return cityPrice.Price;
        }

        // PRIORITY 3: Base Price (Destination City)
        // This is the fallback if no intermediate price is found
        var basePrice = relevantPrices
            .Where(p => p.CityId == destinationId && p.DirectionId == null)
            .FirstOrDefault();

        return basePrice?.Price;
    }
}