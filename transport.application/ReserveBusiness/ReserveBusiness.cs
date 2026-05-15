using MercadoPago.Client.Common;
using MercadoPago.Client.Payment;
using MercadoPago.Resource.Payment;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq;
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
    private readonly ITenantContext _tenantContext;
    private readonly ICustomerBusiness _customerBusiness;
    private readonly IReserveOption _reserveOptions;
    private readonly ICashBoxBusiness _cashBoxBusiness;
    private readonly IReserveSlotLockBusiness _slotLockBusiness;

    public ReserveBusiness(IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        ITenantContext tenantContext,
        IMercadoPagoPaymentGateway paymentGateway,
        ICustomerBusiness customerBusiness,
        IReserveOption reserveOptions,
        ICashBoxBusiness cashBoxBusiness,
        IReserveSlotLockBusiness slotLockBusiness)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _tenantContext = tenantContext;
        _paymentGateway = paymentGateway;
        _customerBusiness = customerBusiness;
        _reserveOptions = reserveOptions;
        _cashBoxBusiness = cashBoxBusiness;
        _slotLockBusiness = slotLockBusiness;
    }

    public async Task<Result<int>> CreateReserve(ReserveCreateDto dto)
    {
        // Validate Vehicle
        var vehicle = await _context.Vehicles.Where(x => x.VehicleId == dto.VehicleId).FirstOrDefaultAsync();
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
            var driver = await _context.Drivers.Where(x => x.DriverId == dto.DriverId.Value).FirstOrDefaultAsync();
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

    //TODO: Se puede mejorar sin mandar lista de items por reserva.
    //Ida: $x
    //IdaVuelta: $x (sale más barato)
    //Ida: $x

    public async Task<Result<bool>> CreatePassengerReserves(PassengerReserveCreateRequestWrapperDto request)
    {
        // 1) Payer (Customer) desde el primer pasajero
        var payerCustomerId = request.Passengers.FirstOrDefault()?.CustomerId;
        if (payerCustomerId is null || payerCustomerId == 0)
            return Result.Failure<bool>(Error.Validation(
                "PassengerReserveCreateRequestWrapperDto.CustomerIdRequired",
                "CustomerId is required in at least one passenger."));

        var payer = await _context.Customers.Where(x => x.CustomerId == payerCustomerId).FirstOrDefaultAsync();
        if (payer is null)
            return Result.Failure<bool>(CustomerError.NotFound);

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var requestedType = (ReserveTypeIdEnum)request.ReserveTypeId;
            var effectiveType = await ResolveEffectiveReserveTypeAsync(
                requestedType, request.OutboundReserveId, request.ReturnReserveId);

            var outboundReserve = await _context.Reserves
                .Include(r => r.Passengers)
                .Include(r => r.Driver)
                .Include(r => r.Trip)
                .SingleOrDefaultAsync(r => r.ReserveId == request.OutboundReserveId);

            if (outboundReserve is null) return Result.Failure<bool>(ReserveError.NotFound);
            if (outboundReserve.Status != ReserveStatusEnum.Confirmed) return Result.Failure<bool>(ReserveError.NotAvailable);

            Reserve? returnReserve = null;
            if (request.ReturnReserveId.HasValue)
            {
                returnReserve = await _context.Reserves
                    .Include(r => r.Passengers)
                    .Include(r => r.Driver)
                    .Include(r => r.Trip)
                    .SingleOrDefaultAsync(r => r.ReserveId == request.ReturnReserveId.Value);

                if (returnReserve is null) return Result.Failure<bool>(ReserveError.NotFound);
                if (returnReserve.Status != ReserveStatusEnum.Confirmed) return Result.Failure<bool>(ReserveError.NotAvailable);
            }

            var reserveMap = new Dictionary<int, Reserve> { [outboundReserve.ReserveId] = outboundReserve };
            if (returnReserve is not null) reserveMap[returnReserve.ReserveId] = returnReserve;

            var servicesCache = new Dictionary<int, Service>();
            foreach (var reserve in reserveMap.Values)
            {
                if (reserve.ServiceId.HasValue && !servicesCache.ContainsKey(reserve.ServiceId.Value))
                {
                    var service = await _context.Services
                        .Include(s => s.Trip.OriginCity)
                        .Include(s => s.Trip.DestinationCity)
                        .SingleOrDefaultAsync(s => s.ServiceId == reserve.ServiceId.Value);
                    if (service is null) return Result.Failure<bool>(ServiceError.ServiceNotFound);
                    servicesCache[reserve.ServiceId.Value] = service;
                }
            }

            var passengerCount = request.Passengers.Count;
            var mainReserveId = outboundReserve.ReserveId;

            foreach (var reserve in reserveMap.Values)
            {
                var vehicle = await _context.Vehicles.Where(x => x.VehicleId == reserve.VehicleId).FirstOrDefaultAsync();
                var existingCount = reserve.Passengers.Count;
                if (vehicle == null || existingCount + passengerCount > vehicle.AvailableQuantity)
                    return Result.Failure<bool>(ReserveError.VehicleQuantityNotAvailable(
                        existingCount, passengerCount, vehicle?.AvailableQuantity ?? 0));
            }

            decimal totalExpectedAmount = 0m;

            foreach (var pax in request.Passengers)
            {
                var outboundResult = await CreatePassengerAdmin(
                    outboundReserve, returnReserve?.ReserveId, pax, pax.Outbound, effectiveType, payer);
                if (outboundResult.IsFailure) return Result.Failure<bool>(outboundResult.Error);
                totalExpectedAmount += pax.Outbound.Price;

                if (returnReserve is not null && pax.Return is not null)
                {
                    var returnResult = await CreatePassengerAdmin(
                        returnReserve, outboundReserve.ReserveId, pax, pax.Return, effectiveType, payer);
                    if (returnResult.IsFailure) return Result.Failure<bool>(returnResult.Error);
                    totalExpectedAmount += pax.Return.Price;
                }
            }

            await _context.SaveChangesWithOutboxAsync();

            var reserveIds = reserveMap.Keys.OrderBy(x => x).ToList();
            var description = BuildDescription(reserveIds, reserveMap, servicesCache, requestedType);

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

            if (!request.Payments.Any())
            {
                await _context.SaveChangesWithOutboxAsync();
                return Result.Success(true);
            }

            var totalProvidedAmount = request.Payments.Sum(p => p.TransactionAmount);
            if (totalProvidedAmount > totalExpectedAmount)
                return Result.Failure<bool>(ReserveError.OverPaymentNotAllowed(totalExpectedAmount, totalProvidedAmount));

            var cashBoxResult = await _cashBoxBusiness.GetOpenCashBoxEntity();
            if (cashBoxResult.IsFailure) return Result.Failure<bool>(cashBoxResult.Error);
            var cashBox = cashBoxResult.Value;
            var primaryMethod = (PaymentMethodEnum)request.Payments.First().PaymentMethod;

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
            await _context.SaveChangesWithOutboxAsync();

            if (request.Payments.Count > 1)
            {
                foreach (var p in request.Payments)
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

            var paymentDescription = totalProvidedAmount < totalExpectedAmount
                ? $"Pago parcial aplicado a {description}"
                : $"Pago aplicado a {description}";
            _context.CustomerAccountTransactions.Add(new CustomerAccountTransaction
            {
                CustomerId = payer.CustomerId,
                Date = DateTime.UtcNow,
                Type = TransactionType.Payment,
                Amount = -totalProvidedAmount,
                Description = paymentDescription,
                RelatedReserveId = mainReserveId,
                ReservePaymentId = parentPayment.ReservePaymentId
            });
            payer.CurrentBalance -= totalProvidedAmount;
            _context.Customers.Update(payer);

            if (totalProvidedAmount >= totalExpectedAmount)
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
        });
    }

    private async Task<Result<Passenger>> CreatePassengerAdmin(
        Reserve reserve, int? relatedReserveId, PassengerBookingDto pax, LegInfoDto leg,
        ReserveTypeIdEnum effectiveType, Customer payer)
    {
        var reservePrice = await GetPassengerPriceAsync(
            reserve.Trip.OriginCityId, reserve.Trip.DestinationCityId, effectiveType, leg.DropoffLocationId);

        if (reservePrice is null || leg.Price != reservePrice.Value)
            return Result.Failure<Passenger>(ReserveError.PriceNotAvailable);

        var pickupResult = await GetDirectionAsync(leg.PickupLocationId, "Pickup");
        if (pickupResult.IsFailure) return Result.Failure<Passenger>(pickupResult.Error);

        var dropoffResult = await GetDirectionAsync(leg.DropoffLocationId, "Dropoff");
        if (dropoffResult.IsFailure) return Result.Failure<Passenger>(dropoffResult.Error);

        var passenger = new Passenger
        {
            ReserveId = reserve.ReserveId,
            ReserveRelatedId = relatedReserveId,
            PickupLocationId = leg.PickupLocationId,
            DropoffLocationId = leg.DropoffLocationId,
            PickupAddress = pickupResult.Value?.Name,
            DropoffAddress = dropoffResult.Value?.Name,
            HasTraveled = pax.HasTraveled,
            Price = reservePrice.Value,
            Status = PassengerStatusEnum.PendingPayment,
            CustomerId = payer.CustomerId,
            DocumentNumber = payer.DocumentNumber,
            FirstName = payer.FirstName,
            LastName = payer.LastName,
            Phone = $"{payer.Phone1} / {payer.Phone2}",
            Email = payer.Email
        };

        reserve.Passengers.Add(passenger);
        _context.Passengers.Add(passenger);
        return Result.Success(passenger);
    }

    private async Task<ReserveTypeIdEnum> ResolveEffectiveReserveTypeAsync(
        ReserveTypeIdEnum requestedType, int outboundReserveId, int? returnReserveId)
    {
        if (requestedType != ReserveTypeIdEnum.IdaVuelta || returnReserveId is null)
            return requestedType;

        var config = await _context.TenantConfigs
            .Where(c => c.TenantId == _tenantContext.TenantId)
            .Select(c => new { c.RoundTripRequiresSameDay })
            .FirstOrDefaultAsync();

            if (config is null || !config.RoundTripRequiresSameDay)
            return ReserveTypeIdEnum.IdaVuelta;

        var dates = await _context.Reserves
            .Where(r => r.ReserveId == outboundReserveId || r.ReserveId == returnReserveId)
            .Select(r => r.ReserveDate.Date)
            .ToListAsync();

        return dates.Distinct().Count() == 1
            ? ReserveTypeIdEnum.IdaVuelta
            : ReserveTypeIdEnum.Ida;
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
        PassengerReserveCreateRequestWrapperExternalDto request)
    {
        if (request.Passengers is null || request.Passengers.Count == 0)
            return Result.Failure<CreateReserveExternalResult>(
                ReserveError.InvalidReserveCombination("No hay pasajeros."));

        User userLogged = null;
        Customer bookingCustomer = null;

        if (_userContext.UserId != null && _userContext.UserId > 0)
        {
            userLogged = await _context.Users
                .Include(u => u.Customer)
                .SingleOrDefaultAsync(u => u.UserId == _userContext.UserId);

            bookingCustomer = userLogged?.Customer;
        }

        var requestedType = (ReserveTypeIdEnum)request.ReserveTypeId;
        var effectiveType = await ResolveEffectiveReserveTypeAsync(
            requestedType, request.OutboundReserveId, request.ReturnReserveId);

        var outboundReserve = await _context.Reserves
            .Include(r => r.Passengers)
            .Include(r => r.Trip)
            .SingleOrDefaultAsync(r => r.ReserveId == request.OutboundReserveId);

        if (outboundReserve is null) return Result.Failure<CreateReserveExternalResult>(ReserveError.NotFound);
        if (outboundReserve.Status != ReserveStatusEnum.Confirmed)
            return Result.Failure<CreateReserveExternalResult>(ReserveError.NotAvailable);

        Reserve? returnReserve = null;
        if (request.ReturnReserveId.HasValue)
        {
            returnReserve = await _context.Reserves
                .Include(r => r.Passengers)
                .Include(r => r.Trip)
                .SingleOrDefaultAsync(r => r.ReserveId == request.ReturnReserveId.Value);

            if (returnReserve is null) return Result.Failure<CreateReserveExternalResult>(ReserveError.NotFound);
            if (returnReserve.Status != ReserveStatusEnum.Confirmed)
                return Result.Failure<CreateReserveExternalResult>(ReserveError.NotAvailable);
        }

        var reserveList = new List<Reserve> { outboundReserve };
        if (returnReserve is not null) reserveList.Add(returnReserve);

        var passengerCount = request.Passengers.Count;

        foreach (var reserve in reserveList)
        {
            var vehicle = await _context.Vehicles.Where(x => x.VehicleId == reserve.VehicleId).FirstOrDefaultAsync();
            var existingCount = reserve.Passengers.Count;
            if (vehicle == null || existingCount + passengerCount > vehicle.AvailableQuantity)
                return Result.Failure<CreateReserveExternalResult>(
                    ReserveError.VehicleQuantityNotAvailable(existingCount, passengerCount, vehicle?.AvailableQuantity ?? 0));
        }

        decimal totalExpectedAmount = 0m;
        var preferenceItems = new List<PaymentPreferenceItemDto>();

        foreach (var pax in request.Passengers)
        {
            var outboundResult = await CreatePassengerExternal(
                outboundReserve, returnReserve?.ReserveId, pax, pax.Outbound, effectiveType,
                isPaid: request.Payment is not null);
            if (outboundResult.IsFailure) return Result.Failure<CreateReserveExternalResult>(outboundResult.Error);
            totalExpectedAmount += pax.Outbound.Price;
            preferenceItems.Add(new PaymentPreferenceItemDto(
                $"Pasaje de {pax.FirstName} {pax.LastName}",
                pax.Outbound.Price,
                $"Reserva {outboundReserve.ReserveId}"));

            if (returnReserve is not null && pax.Return is not null)
            {
                var returnResult = await CreatePassengerExternal(
                    returnReserve, outboundReserve.ReserveId, pax, pax.Return, effectiveType,
                    isPaid: request.Payment is not null);
                if (returnResult.IsFailure) return Result.Failure<CreateReserveExternalResult>(returnResult.Error);
                totalExpectedAmount += pax.Return.Price;
                preferenceItems.Add(new PaymentPreferenceItemDto(
                    $"Pasaje de {pax.FirstName} {pax.LastName}",
                    pax.Return.Price,
                    $"Reserva {returnReserve.ReserveId}"));
            }
        }

        await _context.SaveChangesWithOutboxAsync();

        if (request.Payment is null)
        {
            var firstPax = request.Passengers.First();
            var resultPayment = await CreatePendingPayment(totalExpectedAmount, reserveList, firstPax);
            if (resultPayment.IsFailure)
                return Result.Failure<CreateReserveExternalResult>(resultPayment.Error);

            string preferenceId = await _paymentGateway.CreatePreferenceAsync(
                resultPayment.Value.ToString(),
                totalExpectedAmount,
                preferenceItems
            );

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(new CreateReserveExternalResult(PaymentStatus.Pending, preferenceId));
        }
        else
        {
            var totalProvidedAmount = request.Payment.TransactionAmount;

            if (totalExpectedAmount != totalProvidedAmount)
                return Result.Failure<CreateReserveExternalResult>(
                    ReserveError.InvalidPaymentAmount(totalExpectedAmount, totalProvidedAmount));

            var resultPayment = await CreatePayment(request.Payment, reserveList);
            if (resultPayment.IsFailure)
                return Result.Failure<CreateReserveExternalResult>(resultPayment.Error);

            var mainReserve = reserveList.OrderBy(r => r.ReserveId).First();
            var firstPassenger = request.Passengers.First();
            var reserveCreatedEvent = new CustomerReserveCreatedEvent(
                ReserveId: mainReserve.ReserveId,
                TenantId: mainReserve.TenantId,
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

    private async Task<Result<Passenger>> CreatePassengerExternal(
        Reserve reserve, int? relatedReserveId, PassengerBookingExternalDto pax, LegInfoDto leg,
        ReserveTypeIdEnum effectiveType, bool isPaid)
    {
        var reservePrice = await GetPassengerPriceAsync(
            reserve.Trip.OriginCityId, reserve.Trip.DestinationCityId, effectiveType, leg.DropoffLocationId);

        if (reservePrice is null || leg.Price != reservePrice.Value)
            return Result.Failure<Passenger>(ReserveError.PriceNotAvailable);

        if (reserve.Passengers.Any(p => p.DocumentNumber == pax.DocumentNumber))
            return Result.Failure<Passenger>(ReserveError.PassengerAlreadyExists(pax.DocumentNumber));

        var pickupResult = await GetDirectionAsync(leg.PickupLocationId, "Pickup");
        if (pickupResult.IsFailure) return Result.Failure<Passenger>(pickupResult.Error);

        var dropoffResult = await GetDirectionAsync(leg.DropoffLocationId, "Dropoff");
        if (dropoffResult.IsFailure) return Result.Failure<Passenger>(dropoffResult.Error);

        var existingCustomer = await _context.Customers
            .SingleOrDefaultAsync(c => c.DocumentNumber == pax.DocumentNumber);

        var newPassenger = new Passenger
        {
            ReserveId = reserve.ReserveId,
            ReserveRelatedId = relatedReserveId,
            FirstName = pax.FirstName,
            LastName = pax.LastName,
            DocumentNumber = pax.DocumentNumber,
            Email = pax.Email,
            Phone = pax.Phone1,
            PickupLocationId = leg.PickupLocationId,
            DropoffLocationId = leg.DropoffLocationId,
            PickupAddress = pickupResult.Value?.Name,
            DropoffAddress = dropoffResult.Value?.Name,
            HasTraveled = false,
            Price = reservePrice.Value,
            Status = isPaid ? PassengerStatusEnum.Confirmed : PassengerStatusEnum.PendingPayment,
            CustomerId = existingCustomer?.CustomerId,
        };

        reserve.Passengers.Add(newPassenger);
        _context.Passengers.Add(newPassenger);
        return Result.Success(newPassenger);
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
      PassengerBookingExternalDto firstPassenger)
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
        Dictionary<int, Service> servicesCache, ReserveTypeIdEnum reserveType)
    {
        if (reserveIds.Count == 1)
        {
            var rid = reserveIds[0];
            var reserve = reserveMap[rid];
            var originName = reserve.ServiceId.HasValue && servicesCache.TryGetValue(reserve.ServiceId.Value, out var svc)
                ? svc.Trip.OriginCity.Name : reserve.OriginName;
            var destName = reserve.ServiceId.HasValue && servicesCache.TryGetValue(reserve.ServiceId.Value, out svc)
                ? svc.Trip.DestinationCity.Name : reserve.DestinationName;
            var type = reserveType == ReserveTypeIdEnum.IdaVuelta ? "Ida y vuelta" : "Ida";
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

    private async Task<Result<Direction?>> GetDirectionAsync(int? locationId, string type)
    {
        if (locationId is null || locationId == 0)
            return Result.Success<Direction?>(null);

        var direction = await _context.Directions.Where(x => x.DirectionId == locationId).FirstOrDefaultAsync();

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

    public async Task<Result<bool>> ProcessPaymentFromWebhook(ExternalPaymentResultDto externalPayment)
    {
        // Si ya se actualizó, salir rápido
        if (_context.ReservePayments.Any(p => p.PaymentExternalId == externalPayment.PaymentExternalId
            && p.Status != StatusPaymentEnum.Pending))
        {
            return Result.Success(true);
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // ExternalReference = ReservePaymentId (del padre)
            var parentPayment = await _context.ReservePayments
                .FirstOrDefaultAsync(rp => rp.ReservePaymentId == int.Parse(externalPayment.ExternalReference));

            if (parentPayment == null)
                return Result.Failure<bool>(Error.NotFound("Payment.NotFound",
                    "No se encontró el pago con el ID externo proporcionado"));

            var internalStatus = GetPaymentStatusFromExternal(externalPayment.Status);
            if (internalStatus == null)
                return Result.Failure<bool>(Error.Validation("Payment.InvalidStatus",
                    "Estado de pago no reconocido"));

            // Actualizar padre
            parentPayment.Status = internalStatus.Value;
            parentPayment.StatusDetail = externalPayment.StatusDetail;
            parentPayment.PaymentExternalId = externalPayment.PaymentExternalId;
            parentPayment.ResultApiExternalRawJson = externalPayment.RawJson;
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

    public async Task<Result<bool>> UpdateReserveAsync(int reserveId, ReserveUpdateRequestDto request)
    {
        var reserve = await _context.Reserves
            .FirstOrDefaultAsync(r => r.ReserveId == reserveId);

        if (reserve is null)
            return Result.Failure<bool>(ReserveError.NotFound);

        if (request.VehicleId != null)
        {
            var vehicle = await _context.Vehicles.Where(x => x.VehicleId == request.VehicleId).FirstOrDefaultAsync();
            if (vehicle is null)
                return Result.Failure<bool>(VehicleError.VehicleNotFound);
            reserve.VehicleId = request.VehicleId.Value;
        }

        if (request.DriverId != null)
        {
            var driver = await _context.Drivers.Where(x => x.DriverId == request.DriverId).FirstOrDefaultAsync();
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
            var pickup = await _context.Directions.Where(x => x.DirectionId == request.PickupLocationId).FirstOrDefaultAsync();
            if (pickup == null)
                return Result.Failure<bool>(Error.NotFound("Pickup.NotFound", "Pickup location not found"));

            passenger.PickupLocationId = pickup.DirectionId;
            passenger.PickupAddress = pickup.Name;
        }

        if (request.DropoffLocationId.HasValue)
        {
            var dropoff = await _context.Directions.Where(x => x.DirectionId == request.DropoffLocationId).FirstOrDefaultAsync();
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
            var totalPassengerPrice = reserve.Passengers.Sum(p => p.Price);
            var providedAmount = payments.Sum(p => p.TransactionAmount);

            // Calcular pagos ya realizados (padres con status Paid)
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

            // Solo confirmar pasajeros si la deuda queda completamente saldada
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

    public async Task<Result<bool>> SettleCustomerDebtAsync(SettleCustomerDebtRequestDto request)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // 1. Validar customer existe
            var customer = await _context.Customers.Where(x => x.CustomerId == request.CustomerId).FirstOrDefaultAsync();
            if (customer is null)
                return Result.Failure<bool>(CustomerError.NotFound);

            // 2. Validar pagos (montos > 0, sin metodos duplicados)
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

            // 3. Cargar reservas con pasajeros del customer
            var reserves = await _context.Reserves
                .Include(r => r.Passengers)
                .Where(r => request.ReserveIds.Contains(r.ReserveId))
                .ToListAsync();

            if (!reserves.Any())
                return Result.Failure<bool>(ReserveError.NotFound);

            // 4. Para cada reserva: calcular deuda pendiente
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

            // 5. Si todas estan pagadas -> error
            if (!reserveDebts.Any())
                return Result.Failure<bool>(ReserveError.NoDebtToSettle);

            // 6. Validar totalPayment <= totalDebtAcrossReserves
            var totalPayment = request.Payments.Sum(p => p.TransactionAmount);
            var totalDebt = reserveDebts.Sum(rd => rd.Debt);

            if (totalPayment > totalDebt)
                return Result.Failure<bool>(ReserveError.OverPaymentNotAllowed(totalDebt, totalPayment));

            // 7. Obtener CashBox abierta
            var cashBoxResult = await _cashBoxBusiness.GetOpenCashBoxEntity();
            if (cashBoxResult.IsFailure)
                return Result.Failure<bool>(cashBoxResult.Error);

            var cashBox = cashBoxResult.Value;
            var mainReserveId = reserveDebts.First().Reserve.ReserveId;
            var primaryMethod = (PaymentMethodEnum)request.Payments.First().PaymentMethod;

            // 8. Crear ReservePayment padre
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

            // 9. Si multi-metodo, crear hijos de desglose
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

            // 10. Crear transaccion Payment
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

            // 11. Actualizar Customer.CurrentBalance
            customer.CurrentBalance -= totalPayment;
            _context.Customers.Update(customer);

            // 12. Distribuir pago secuencialmente por reservas
            var remainingPayment = totalPayment;
            foreach (var (reserve, debt) in reserveDebts)
            {
                if (remainingPayment <= 0) break;

                var appliedToReserve = Math.Min(remainingPayment, debt);
                remainingPayment -= appliedToReserve;

                // Si esta reserva queda saldada, confirmar pasajeros
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

    public async Task<Result<List<CustomerPendingReserveDto>>> GetCustomerPendingReservesAsync(int customerId)
    {
        var customer = await _context.Customers.Where(x => x.CustomerId == customerId).FirstOrDefaultAsync();
        if (customer is null)
            return Result.Failure<List<CustomerPendingReserveDto>>(CustomerError.NotFound);

        // Obtener todas las reservas donde el cliente tiene pasajeros con deuda
        var reserves = await _context.Reserves
            .AsNoTracking()
            .Include(r => r.Passengers)
            .Where(r => r.Passengers.Any(p => p.CustomerId == customerId
                && p.Status == PassengerStatusEnum.PendingPayment))
            .ToListAsync();

        if (!reserves.Any())
            return Result.Success(new List<CustomerPendingReserveDto>());

        var result = new List<CustomerPendingReserveDto>();

        foreach (var reserve in reserves)
        {
            var customerPassengers = reserve.Passengers
                .Where(p => p.CustomerId == customerId)
                .ToList();

            var totalPrice = customerPassengers.Sum(p => p.Price);

            var totalPaid = await _context.ReservePayments
                .AsNoTracking()
                .Where(rp => rp.ReserveId == reserve.ReserveId
                    && rp.CustomerId == customerId
                    && rp.ParentReservePaymentId == null
                    && rp.Status == StatusPaymentEnum.Paid)
                .SumAsync(rp => rp.Amount);

            var pendingDebt = totalPrice - totalPaid;
            if (pendingDebt <= 0) continue;

            result.Add(new CustomerPendingReserveDto(
                reserve.ReserveId,
                reserve.ReserveDate,
                reserve.OriginName,
                reserve.DestinationName,
                reserve.DepartureHour.ToString(@"hh\:mm"),
                totalPrice,
                totalPaid,
                pendingDebt,
                customerPassengers.Select(p => new CustomerPendingPassengerDto(
                    p.PassengerId,
                    $"{p.FirstName} {p.LastName}",
                    p.Price,
                    (int)p.Status
                )).ToList()
            ));
        }

        return Result.Success(result);
    }

    public async Task<Result<CreateReserveExternalResult>> CreatePassengerReservesWithLock(CreateReserveWithLockRequestDto request)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var validation = await _slotLockBusiness.ValidateAsync(
                request.LockToken,
                request.OutboundReserveId,
                request.ReturnReserveId,
                request.Passengers.Count);

            if (validation.IsFailure)
                return Result.Failure<CreateReserveExternalResult>(validation.Error);

            var externalDto = new PassengerReserveCreateRequestWrapperExternalDto(
                request.ReserveTypeId,
                request.OutboundReserveId,
                request.ReturnReserveId,
                request.Payment,
                request.Passengers
            );

            var result = await CreatePassengerReservesExternalCore(externalDto);

            if (result.IsSuccess)
            {
                await _slotLockBusiness.MarkAsUsedAsync(validation.Value);
            }

            return result;
        });
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
            var dropoffDirection = await _context.Directions.Where(x => x.DirectionId == dropoffLocationId.Value).FirstOrDefaultAsync();
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