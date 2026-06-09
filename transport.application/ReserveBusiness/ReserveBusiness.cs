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
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReserveBusiness(IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        ITenantContext tenantContext,
        IMercadoPagoPaymentGateway paymentGateway,
        ICustomerBusiness customerBusiness,
        IReserveOption reserveOptions,
        ICashBoxBusiness cashBoxBusiness,
        IReserveSlotLockBusiness slotLockBusiness,
        IDateTimeProvider dateTimeProvider)
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
        _dateTimeProvider = dateTimeProvider;
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

        var slotTaken = await _context.Reserves.AnyAsync(r =>
            r.TripId == trip.TripId &&
            r.ReserveDate.Date == dto.ReserveDate.Date &&
            r.DepartureHour == dto.DepartureHour &&
            r.Status != ReserveStatusEnum.Cancelled &&
            r.Status != ReserveStatusEnum.Expired);

        if (slotTaken)
            return Result.Failure<int>(ReserveError.SlotAlreadyTaken(trip.TripId, dto.ReserveDate, dto.DepartureHour));

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
            ServiceId = null
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

            // Pares de piernas IdaVuelta de una misma persona, para linkear vía RelatedPassengerId
            // recién DESPUÉS del primer SaveChanges: ambos Passenger son [Added] y referenciarse
            // entre sí antes de existir en la base genera una dependencia circular que EF rechaza
            // ("Unable to save changes because a circular dependency was detected").
            var relatedPassengerPairs = new List<(Passenger Outbound, Passenger Return)>();

            foreach (var pax in request.Passengers)
            {
                // Price validation depends on the EFFECTIVE reserve type:
                // - IdaVuelta (same-day round-trip with discount): the TripPrice IdaVuelta is the
                //   TOTAL package price; lookup ONCE on the outbound trip and validate that the sum
                //   Outbound.Price + Return.Price matches. Frontend can split arbitrarily (50/50, etc.).
                // - Ida (single leg or downgraded round-trip): each leg validates independently
                //   against the Ida tariff of its own Trip.
                if (effectiveType == ReserveTypeIdEnum.IdaVuelta && returnReserve is not null && pax.Return is not null)
                {
                    var packagePrice = await GetPassengerPriceAsync(
                        outboundReserve.Trip.OriginCityId, outboundReserve.Trip.DestinationCityId,
                        ReserveTypeIdEnum.IdaVuelta, pax.Outbound.DropoffLocationId);

                    if (packagePrice is null || pax.Outbound.Price + pax.Return.Price != packagePrice.Value)
                        return Result.Failure<bool>(ReserveError.PriceNotAvailable);

                    totalExpectedAmount += packagePrice.Value;
                }
                else
                {
                    var outboundExpected = await GetPassengerPriceAsync(
                        outboundReserve.Trip.OriginCityId, outboundReserve.Trip.DestinationCityId,
                        effectiveType, pax.Outbound.DropoffLocationId);

                    if (outboundExpected is null || pax.Outbound.Price != outboundExpected.Value)
                        return Result.Failure<bool>(ReserveError.PriceNotAvailable);

                    totalExpectedAmount += pax.Outbound.Price;

                    if (returnReserve is not null && pax.Return is not null)
                    {
                        var returnExpected = await GetPassengerPriceAsync(
                            returnReserve.Trip.OriginCityId, returnReserve.Trip.DestinationCityId,
                            effectiveType, pax.Return.DropoffLocationId);

                        if (returnExpected is null || pax.Return.Price != returnExpected.Value)
                            return Result.Failure<bool>(ReserveError.PriceNotAvailable);

                        totalExpectedAmount += pax.Return.Price;
                    }
                }

                var outboundResult = await CreatePassengerAdminEntity(
                    outboundReserve, returnReserve?.ReserveId, pax, pax.Outbound, payer);
                if (outboundResult.IsFailure) return Result.Failure<bool>(outboundResult.Error);

                if (returnReserve is not null && pax.Return is not null)
                {
                    var returnResult = await CreatePassengerAdminEntity(
                        returnReserve, outboundReserve.ReserveId, pax, pax.Return, payer);
                    if (returnResult.IsFailure) return Result.Failure<bool>(returnResult.Error);

                    // Vínculo pasajero↔pasajero (las dos piernas de esta misma persona). Se resuelve
                    // después del primer SaveChanges, una vez que ambos tienen PassengerId real.
                    relatedPassengerPairs.Add((outboundResult.Value, returnResult.Value));
                }
            }

            await _context.SaveChangesWithOutboxAsync();

            foreach (var (outboundPassenger, returnPassenger) in relatedPassengerPairs)
            {
                outboundPassenger.RelatedPassengerId = returnPassenger.PassengerId;
                returnPassenger.RelatedPassengerId = outboundPassenger.PassengerId;
            }

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

    /// <summary>
    /// Creates a Passenger entity for a leg. Price validation is performed by the caller
    /// (CreatePassengerReserves) because the validation strategy differs between Ida (per leg)
    /// and IdaVuelta (sum of legs against package price).
    /// </summary>
    private async Task<Result<Passenger>> CreatePassengerAdminEntity(
        Reserve reserve, int? relatedReserveId, PassengerBookingDto pax, LegInfoDto leg, Customer payer)
    {
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
            Price = leg.Price,
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

        // El alta de usuario final solo registra al CLIENTE QUE PAGA (IsPayment); los
        // acompañantes quedan como Passenger sin Customer. Si el pagador todavía no existe
        // (compra de un cliente nuevo) se da de alta ACÁ, antes del loop de pasajeros y del
        // pago: el resto del flujo lo vincula buscándolo por DocumentNumber
        // (CreatePassengerExternalEntity y CreatePayment/CreatePendingPayment), así que con
        // crearlo y persistirlo alcanza para que esos lookups lo encuentren.
        var payerPax = request.Passengers.FirstOrDefault(p => p.IsPayment) ?? request.Passengers.First();
        var existingPayer = await _context.Customers
            .SingleOrDefaultAsync(c => c.DocumentNumber == payerPax.DocumentNumber);

        if (existingPayer is null)
        {
            _context.Customers.Add(new Customer
            {
                FirstName = payerPax.FirstName,
                LastName = payerPax.LastName,
                DocumentNumber = payerPax.DocumentNumber,
                Email = payerPax.Email ?? request.Payment?.PayerEmail ?? string.Empty,
                Phone1 = payerPax.Phone1,
            });
            await _context.SaveChangesWithOutboxAsync();
        }

        decimal totalExpectedAmount = 0m;
        var preferenceItems = new List<PaymentPreferenceItemDto>();

        // Pares de piernas IdaVuelta de una misma persona, para linkear vía RelatedPassengerId
        // recién DESPUÉS del primer SaveChanges: ambos Passenger son [Added] y referenciarse
        // entre sí antes de existir en la base genera una dependencia circular que EF rechaza
        // ("Unable to save changes because a circular dependency was detected").
        var relatedPassengerPairs = new List<(Passenger Outbound, Passenger Return)>();

        // Pasajeros creados en ESTE booking, para imputarles luego el ReservePayment padre
        // (vínculo al pagador). No usar reserve.Passengers: una Reserve puede tener pasajeros
        // de otros bookings/pagadores.
        var bookingPassengers = new List<Passenger>();

        foreach (var pax in request.Passengers)
        {
            // Same pricing model as the admin flow:
            // - IdaVuelta (effective): TripPrice is the package total. Validate sum of legs.
            // - Ida (effective or downgraded): validate each leg against its own Trip's Ida tariff.
            if (effectiveType == ReserveTypeIdEnum.IdaVuelta && returnReserve is not null && pax.Return is not null)
            {
                var packagePrice = await GetPassengerPriceAsync(
                    outboundReserve.Trip.OriginCityId, outboundReserve.Trip.DestinationCityId,
                    ReserveTypeIdEnum.IdaVuelta, pax.Outbound.DropoffLocationId);

                if (packagePrice is null || pax.Outbound.Price + pax.Return.Price != packagePrice.Value)
                    return Result.Failure<CreateReserveExternalResult>(ReserveError.PriceNotAvailable);

                totalExpectedAmount += packagePrice.Value;
            }
            else
            {
                var outboundExpected = await GetPassengerPriceAsync(
                    outboundReserve.Trip.OriginCityId, outboundReserve.Trip.DestinationCityId,
                    effectiveType, pax.Outbound.DropoffLocationId);

                if (outboundExpected is null || pax.Outbound.Price != outboundExpected.Value)
                    return Result.Failure<CreateReserveExternalResult>(ReserveError.PriceNotAvailable);

                totalExpectedAmount += pax.Outbound.Price;

                if (returnReserve is not null && pax.Return is not null)
                {
                    var returnExpected = await GetPassengerPriceAsync(
                        returnReserve.Trip.OriginCityId, returnReserve.Trip.DestinationCityId,
                        effectiveType, pax.Return.DropoffLocationId);

                    if (returnExpected is null || pax.Return.Price != returnExpected.Value)
                        return Result.Failure<CreateReserveExternalResult>(ReserveError.PriceNotAvailable);

                    totalExpectedAmount += pax.Return.Price;
                }
            }

            var outboundResult = await CreatePassengerExternalEntity(
                outboundReserve, returnReserve?.ReserveId, pax, pax.Outbound,
                isPaid: request.Payment is not null);
            if (outboundResult.IsFailure) return Result.Failure<CreateReserveExternalResult>(outboundResult.Error);
            bookingPassengers.Add(outboundResult.Value);
            preferenceItems.Add(new PaymentPreferenceItemDto(
                $"Pasaje de {pax.FirstName} {pax.LastName}",
                pax.Outbound.Price,
                $"Reserva {outboundReserve.ReserveId}"));

            if (returnReserve is not null && pax.Return is not null)
            {
                var returnResult = await CreatePassengerExternalEntity(
                    returnReserve, outboundReserve.ReserveId, pax, pax.Return,
                    isPaid: request.Payment is not null);
                if (returnResult.IsFailure) return Result.Failure<CreateReserveExternalResult>(returnResult.Error);
                bookingPassengers.Add(returnResult.Value);
                preferenceItems.Add(new PaymentPreferenceItemDto(
                    $"Pasaje de {pax.FirstName} {pax.LastName}",
                    pax.Return.Price,
                    $"Reserva {returnReserve.ReserveId}"));

                // Vínculo pasajero↔pasajero (las dos piernas de esta misma persona). Se resuelve
                // después del primer SaveChanges, una vez que ambos tienen PassengerId real.
                relatedPassengerPairs.Add((outboundResult.Value, returnResult.Value));
            }
        }

        await _context.SaveChangesWithOutboxAsync();

        foreach (var (outboundPassenger, returnPassenger) in relatedPassengerPairs)
        {
            outboundPassenger.RelatedPassengerId = returnPassenger.PassengerId;
            returnPassenger.RelatedPassengerId = outboundPassenger.PassengerId;
        }

        if (request.Payment is null)
        {
            var firstPax = request.Passengers.First();
            var resultPayment = await CreatePendingPayment(totalExpectedAmount, reserveList, firstPax);
            if (resultPayment.IsFailure)
                return Result.Failure<CreateReserveExternalResult>(resultPayment.Error);

            LinkPassengersToPayment(bookingPassengers, resultPayment.Value);

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

            LinkPassengersToPayment(bookingPassengers, resultPayment.Value);

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

    /// <summary>
    /// Creates a Passenger entity for the external (public) flow. Price validation is performed
    /// by CreatePassengerReservesExternalCore.
    /// </summary>
    private async Task<Result<Passenger>> CreatePassengerExternalEntity(
        Reserve reserve, int? relatedReserveId, PassengerBookingExternalDto pax, LegInfoDto leg, bool isPaid)
    {
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
            Price = leg.Price,
            Status = isPaid ? PassengerStatusEnum.Confirmed : PassengerStatusEnum.PendingPayment,
            CustomerId = existingCustomer?.CustomerId,
        };

        reserve.Passengers.Add(newPassenger);
        _context.Passengers.Add(newPassenger);
        return Result.Success(newPassenger);
    }

    private async Task<Result<int>> CreatePayment(CreatePaymentExternalRequestDto paymentData, List<Reserve> reserves)
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
            return Result.Failure<int>(
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
        return Result.Success(parentPayment.ReservePaymentId);
    }

    /// <summary>
    /// Imputa el ReservePayment (padre) a cada pasajero del booking, dejando el vínculo al pagador
    /// (ReservePayment.CustomerId). Lo usa el refund del cancel cuando el pasajero no tiene Customer
    /// propio (acompañante del alta de usuario final).
    /// </summary>
    private void LinkPassengersToPayment(IEnumerable<Passenger> passengers, int reservePaymentId)
    {
        foreach (var passenger in passengers)
        {
            passenger.ReservePaymentId = reservePaymentId;
            _context.Passengers.Update(passenger);
        }
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


    public async Task<Result<bool>> UpdateReserveAsync(int reserveId, ReserveUpdateRequestDto request)
    {
        var reserve = await _context.Reserves
            .Include(r => r.Passengers)
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
            var newStatus = (ReserveStatusEnum)request.Status;

            // Guard: no permitir cancelar una Reserve que todavía tiene pasajeros activos.
            // El operador debe cancelar primero a cada pasajero (ver ADR-0005). Alcance por-Reserve:
            // en un IdaVuelta, cancelar una pierna no toca la otra.
            if (newStatus == ReserveStatusEnum.Cancelled &&
                reserve.Status != ReserveStatusEnum.Cancelled)
            {
                var activeCount = reserve.Passengers.Count(p =>
                    p.Status == PassengerStatusEnum.PendingPayment ||
                    p.Status == PassengerStatusEnum.Confirmed);

                if (activeCount > 0)
                    return Result.Failure<bool>(ReserveError.HasActivePassengers(activeCount));
            }

            reserve.Status = newStatus;
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

    /// <summary>
    /// Cancela un Passenger individual: lo pasa a Cancelled y revierte su deuda
    /// (CustomerAccountTransaction tipo Refund por -Price, baja CurrentBalance). NO toca la Caja:
    /// la plata cobrada ya ingresó (regla "caja en cero"); si ya estaba pago, el Refund deja saldo
    /// a favor. En un par IdaVuelta cancela ambas piernas (vía RelatedPassengerId) revirtiendo el
    /// packagePrice una sola vez. Solo aplica a pasajeros activos cuya reserva no partió.
    /// </summary>
    public async Task<Result<bool>> CancelPassengerAsync(int passengerId)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var passenger = await _context.Passengers
                .Include(p => p.Reserve)
                .SingleOrDefaultAsync(p => p.PassengerId == passengerId);

            if (passenger is null)
                return Result.Failure<bool>(PassengerError.NotFound);

            var eligibility = ValidatePassengerCancelable(passenger);
            if (eligibility.IsFailure)
                return Result.Failure<bool>(eligibility.Error);

            // Reunir las piernas a cancelar: este Passenger y, si es IdaVuelta, su pierna hermana.
            // El vínculo es directo pasajero↔pasajero (RelatedPassengerId), no por CustomerId: un
            // mismo cliente puede pagar varios pasajeros IdaVuelta y el match por CustomerId
            // cancelaría la pierna de la persona equivocada.
            var passengersToCancel = new List<Passenger> { passenger };
            if (passenger.RelatedPassengerId.HasValue)
            {
                var related = await _context.Passengers
                    .Include(p => p.Reserve)
                    .Where(p => p.PassengerId == passenger.RelatedPassengerId.Value
                             && p.Status != PassengerStatusEnum.Cancelled)
                    .FirstOrDefaultAsync();

                if (related is not null)
                    passengersToCancel.Add(related);
            }

            await CancelPassengersAndRevertDebtAsync(passengersToCancel);

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    /// <summary>
    /// Valida que un Passenger sea cancelable: debe estar activo (PendingPayment o Confirmed) y
    /// su Reserve no haber partido (ReserveDate.Date + DepartureHour > LocalNow).
    /// </summary>
    private Result ValidatePassengerCancelable(Passenger passenger)
    {
        if (passenger.Status != PassengerStatusEnum.PendingPayment &&
            passenger.Status != PassengerStatusEnum.Confirmed)
            return Result.Failure(PassengerError.NotActive(passenger.Status));

        var departure = passenger.Reserve.ReserveDate.Date + passenger.Reserve.DepartureHour;
        if (departure < _dateTimeProvider.LocalNow)
            return Result.Failure(PassengerError.ReserveDeparted);

        return Result.Success();
    }

    /// <summary>
    /// Pasa cada Passenger a Cancelled y, si tiene Price > 0, genera un Refund por -Price a la
    /// cuenta corriente del cliente (baja CurrentBalance). No toca la Caja. El cargo apunta a la
    /// Reserve del propio passenger (RelatedReserveId) para que la deuda vencida se atribuya bien.
    /// Si el pasajero no tiene Customer propio (acompañante del alta de usuario final), el refund
    /// se imputa al PAGADOR del booking vía ReservePayment.CustomerId.
    /// </summary>
    private async Task CancelPassengersAndRevertDebtAsync(IEnumerable<Passenger> passengers)
    {
        var now = _dateTimeProvider.UtcNow;

        foreach (var passenger in passengers)
        {
            passenger.Status = PassengerStatusEnum.Cancelled;
            _context.Passengers.Update(passenger);

            if (passenger.Price <= 0)
                continue;

            // A quién se le reintegra: el cliente del propio pasajero o, si es un acompañante sin
            // Customer (alta de usuario final), el PAGADOR del booking, que se identifica de forma
            // unívoca por el ReservePayment imputado al asiento (ReservePayment.CustomerId).
            var refundCustomerId = passenger.CustomerId;
            if (refundCustomerId is null && passenger.ReservePaymentId is not null)
            {
                var payment = await _context.ReservePayments
                    .FirstOrDefaultAsync(rp => rp.ReservePaymentId == passenger.ReservePaymentId.Value);
                refundCustomerId = payment?.CustomerId;
            }

            if (refundCustomerId is null)
                continue;

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == refundCustomerId.Value);
            if (customer is null)
                continue;

            _context.CustomerAccountTransactions.Add(new CustomerAccountTransaction
            {
                CustomerId = customer.CustomerId,
                Date = now,
                Type = TransactionType.Refund,
                Amount = -passenger.Price,
                Description = $"Cancelación de pasajero {passenger.FirstName} {passenger.LastName} en reserva {passenger.ReserveId}.",
                RelatedReserveId = passenger.ReserveId
            });
            customer.CurrentBalance -= passenger.Price;
            _context.Customers.Update(customer);
        }
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
