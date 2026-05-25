using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.FrequentSubscriptions;
using Transport.Domain.FrequentSubscriptions.Abstraction;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Trips;
using Transport.SharedKernel;
using Transport.SharedKernel.Configuration;

namespace Transport.Business.FrequentSubscriptionBusiness;

public class FrequentPassengerBusiness : IFrequentPassengerBusiness
{
    private readonly IApplicationDbContext _context;
    private readonly IReserveOption _reserveOption;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<FrequentPassengerBusiness>? _logger;

    public FrequentPassengerBusiness(
        IApplicationDbContext context,
        IReserveOption reserveOption,
        IDateTimeProvider dateTimeProvider,
        ITenantContext tenantContext,
        ILogger<FrequentPassengerBusiness>? logger = null)
    {
        _context = context;
        _reserveOption = reserveOption;
        _dateTimeProvider = dateTimeProvider;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Result<bool>> GenerateFrequentPassengersAsync()
    {
        var today = _dateTimeProvider.UtcNow.Date;
        var windowEnd = today.AddDays(await GetReserveGenerationDaysAsync());

        var subscriptions = await _context.FrequentSubscriptions
            .Where(s => s.Status == EntityStatusEnum.Active
                     && s.StartDate <= windowEnd
                     && (s.EndDate == null || s.EndDate >= today))
            .Include(s => s.Customer)
            .Include(s => s.OutboundService).ThenInclude(svc => svc.Trip)
            .Include(s => s.InboundService!).ThenInclude(svc => svc.Trip)
            .ToListAsync();

        if (subscriptions.Count == 0) return Result.Success(true);

        foreach (var subscription in subscriptions)
        {
            await ProcessSubscription(subscription, today, windowEnd);
        }

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> GenerateForSubscriptionAsync(int frequentSubscriptionId)
    {
        var today = _dateTimeProvider.UtcNow.Date;
        var windowEnd = today.AddDays(await GetReserveGenerationDaysAsync());

        _logger?.LogInformation(
            "FrequentPassenger auto-apply START — subscriptionId={Id}, today={Today}, windowEnd={WindowEnd}",
            frequentSubscriptionId, today, windowEnd);

        var subscription = await _context.FrequentSubscriptions
            .Where(s => s.FrequentSubscriptionId == frequentSubscriptionId
                     && s.Status == EntityStatusEnum.Active
                     && s.StartDate <= windowEnd
                     && (s.EndDate == null || s.EndDate >= today))
            .Include(s => s.Customer)
            .Include(s => s.OutboundService).ThenInclude(svc => svc.Trip)
            .Include(s => s.InboundService!).ThenInclude(svc => svc.Trip)
            .FirstOrDefaultAsync();

        if (subscription is null)
        {
            _logger?.LogWarning(
                "FrequentPassenger auto-apply NO-OP — subscriptionId={Id} no encontrada o fuera de ventana " +
                "[StartDate <= {WindowEnd} AND (EndDate IS NULL OR EndDate >= {Today}) AND Status = Active]. " +
                "Si la sub existe en DB, revisar StartDate/EndDate/Status.",
                frequentSubscriptionId, windowEnd, today);
            return Result.Success(true);
        }

        await ProcessSubscription(subscription, today, windowEnd);
        await _context.SaveChangesWithOutboxAsync();

        _logger?.LogInformation(
            "FrequentPassenger auto-apply END — subscriptionId={Id}",
            frequentSubscriptionId);

        return Result.Success(true);
    }

    private async Task ProcessSubscription(FrequentSubscription subscription, DateTime today, DateTime windowEnd)
    {
        var effectiveStart = subscription.StartDate > today ? subscription.StartDate : today;
        var effectiveEnd = subscription.EndDate.HasValue && subscription.EndDate.Value < windowEnd
            ? subscription.EndDate.Value
            : windowEnd;

        _logger?.LogInformation(
            "ProcessSubscription subscriptionId={Id} type={Type} outboundServiceId={Out} inboundServiceId={In} " +
            "effectiveRange=[{Start:yyyy-MM-dd}, {End:yyyy-MM-dd}]",
            subscription.FrequentSubscriptionId, subscription.ReserveTypeId,
            subscription.OutboundServiceId, subscription.InboundServiceId,
            effectiveStart, effectiveEnd);

        if (effectiveStart > effectiveEnd)
        {
            _logger?.LogWarning(
                "ProcessSubscription subscriptionId={Id} SKIP: effectiveStart > effectiveEnd",
                subscription.FrequentSubscriptionId);
            return;
        }

        var outboundReserves = await _context.Reserves
            .Where(r => r.ServiceId == subscription.OutboundServiceId
                     && r.ReserveDate >= effectiveStart
                     && r.ReserveDate <= effectiveEnd.AddDays(1)
                     && r.Status != ReserveStatusEnum.Cancelled
                     && r.Status != ReserveStatusEnum.Expired)
            .ToListAsync();

        _logger?.LogInformation(
            "ProcessSubscription subscriptionId={Id} outboundReserves encontradas={Count}",
            subscription.FrequentSubscriptionId, outboundReserves.Count);

        var inboundReservesByDate = new Dictionary<DateTime, Reserve>();
        if (subscription.ReserveTypeId == ReserveTypeIdEnum.IdaVuelta && subscription.InboundServiceId.HasValue)
        {
            var inboundReserves = await _context.Reserves
                .Where(r => r.ServiceId == subscription.InboundServiceId.Value
                         && r.ReserveDate >= effectiveStart
                         && r.ReserveDate <= effectiveEnd.AddDays(1)
                         && r.Status != ReserveStatusEnum.Cancelled
                         && r.Status != ReserveStatusEnum.Expired)
                .ToListAsync();
            foreach (var r in inboundReserves)
                inboundReservesByDate[r.ReserveDate.Date] = r;
        }

        var processedInboundIds = new HashSet<int>();

        foreach (var outboundReserve in outboundReserves)
        {
            Reserve? inboundReserve = null;
            if (subscription.ReserveTypeId == ReserveTypeIdEnum.IdaVuelta)
                inboundReservesByDate.TryGetValue(outboundReserve.ReserveDate.Date, out inboundReserve);

            if (inboundReserve is not null)
            {
                await CreatePairedPassengers(subscription, outboundReserve, inboundReserve);
                processedInboundIds.Add(inboundReserve.ReserveId);
            }
            else
            {
                await CreateStandalonePassenger(subscription, outboundReserve, subscription.OutboundService, isOutbound: true);
            }
        }

        // Inbound Reserves cuya fecha no matcheó ninguna Outbound (caso DayOfWeek distinto entre Services):
        // creamos un Passenger Ida-solo sobre el InboundService.
        foreach (var (_, inboundReserve) in inboundReservesByDate)
        {
            if (processedInboundIds.Contains(inboundReserve.ReserveId)) continue;
            await CreateStandalonePassenger(subscription, inboundReserve, subscription.InboundService!, isOutbound: false);
        }
    }

    private async Task CreatePairedPassengers(
        FrequentSubscription subscription, Reserve outboundReserve, Reserve inboundReserve)
    {
        var outboundExists = await PassengerExists(outboundReserve.ReserveId, subscription);
        var inboundExists = await PassengerExists(inboundReserve.ReserveId, subscription);

        // Idempotente y atómico: si cualquier leg ya existe, no rellenamos el faltante.
        // El cargo a cuenta corriente es UNO solo por package, así que un estado parcial
        // implicaría cobrar dos veces o no cobrar al re-correr. Se skipea entero.
        if (outboundExists || inboundExists)
        {
            _logger?.LogDebug(
                "CreatePairedPassengers SKIP (already exists) — subscriptionId={SubId} outboundReserveId={Out} inboundReserveId={In}",
                subscription.FrequentSubscriptionId, outboundReserve.ReserveId, inboundReserve.ReserveId);
            return;
        }

        if (!await HasCapacity(outboundReserve))
        {
            _logger?.LogWarning(
                "CreatePairedPassengers SKIP (outbound capacity full) — subscriptionId={SubId} reserveId={ReserveId}",
                subscription.FrequentSubscriptionId, outboundReserve.ReserveId);
            return;
        }
        if (!await HasCapacity(inboundReserve))
        {
            _logger?.LogWarning(
                "CreatePairedPassengers SKIP (inbound capacity full) — subscriptionId={SubId} reserveId={ReserveId}",
                subscription.FrequentSubscriptionId, inboundReserve.ReserveId);
            return;
        }

        var packagePrice = await GetPriceAsync(
            subscription.OutboundService.Trip.OriginCityId,
            subscription.OutboundService.Trip.DestinationCityId,
            ReserveTypeIdEnum.IdaVuelta,
            subscription.OutboundDropoffLocationId);

        if (packagePrice is null || packagePrice.Value <= 0)
        {
            _logger?.LogInformation(
                "CreatePairedPassengers degrada a 2 Idas — subscriptionId={SubId}: no hay TripPrice IdaVuelta " +
                "para tramo origin={Origin} dest={Dest} dropoff={Dropoff}",
                subscription.FrequentSubscriptionId,
                subscription.OutboundService.Trip.OriginCityId,
                subscription.OutboundService.Trip.DestinationCityId,
                subscription.OutboundDropoffLocationId);

            await CreateStandalonePassenger(subscription, outboundReserve, subscription.OutboundService, isOutbound: true);
            await CreateStandalonePassenger(subscription, inboundReserve, subscription.InboundService!, isOutbound: false);
            return;
        }

        // Convención IdaVuelta package (Opción D):
        //   Outbound.Price = packagePrice (el "dueño" del booking)
        //   Inbound.Price  = 0            (asiento incluido en el package, sin revenue adicional)
        //   Sum = packagePrice = una única transacción Charge.
        // El inbound queda enlazado al outbound vía ReserveRelatedId; cualquier consumer
        // que necesite "el precio real" del leg de vuelta lo deriva siguiendo el ReserveRelatedId.
        var outboundPassenger = BuildPassenger(subscription, outboundReserve, packagePrice.Value,
            pickupLocationId: subscription.OutboundPickupLocationId,
            dropoffLocationId: subscription.OutboundDropoffLocationId,
            relatedReserveId: inboundReserve.ReserveId);
        _context.Passengers.Add(outboundPassenger);

        var inboundPassenger = BuildPassenger(subscription, inboundReserve, 0m,
            pickupLocationId: subscription.InboundPickupLocationId!.Value,
            dropoffLocationId: subscription.InboundDropoffLocationId!.Value,
            relatedReserveId: outboundReserve.ReserveId);
        _context.Passengers.Add(inboundPassenger);

        ChargeCustomer(subscription, packagePrice.Value,
            $"Pasajero frecuente IdaVuelta (suscripción {subscription.FrequentSubscriptionId}) reservas {outboundReserve.ReserveId}/{inboundReserve.ReserveId}.",
            outboundReserve.ReserveId);
    }

    private async Task CreateStandalonePassenger(
        FrequentSubscription subscription, Reserve reserve, Service service, bool isOutbound)
    {
        if (await PassengerExists(reserve.ReserveId, subscription))
        {
            _logger?.LogDebug(
                "CreateStandalonePassenger SKIP (already exists) — subscriptionId={SubId} reserveId={ReserveId}",
                subscription.FrequentSubscriptionId, reserve.ReserveId);
            return;
        }

        if (!await HasCapacity(reserve))
        {
            _logger?.LogWarning(
                "CreateStandalonePassenger SKIP (capacity full) — subscriptionId={SubId} reserveId={ReserveId} vehicleId={VehicleId}",
                subscription.FrequentSubscriptionId, reserve.ReserveId, reserve.VehicleId);
            return;
        }

        var pickup = isOutbound ? subscription.OutboundPickupLocationId : subscription.InboundPickupLocationId!.Value;
        var dropoff = isOutbound ? subscription.OutboundDropoffLocationId : subscription.InboundDropoffLocationId!.Value;

        var price = await GetPriceAsync(
            service.Trip.OriginCityId,
            service.Trip.DestinationCityId,
            ReserveTypeIdEnum.Ida,
            dropoff);

        if (price is null || price.Value <= 0)
        {
            _logger?.LogWarning(
                "CreateStandalonePassenger SKIP (price null/0) — subscriptionId={SubId} reserveId={ReserveId} " +
                "tripId={TripId} originCityId={OriginCity} destCityId={DestCity} dropoffLocationId={Dropoff}. " +
                "Verificar TripPrice configurado (Ida) para ese tramo + dropoff.",
                subscription.FrequentSubscriptionId, reserve.ReserveId,
                service.TripId, service.Trip.OriginCityId, service.Trip.DestinationCityId, dropoff);
            return;
        }

        var passenger = BuildPassenger(subscription, reserve, price.Value,
            pickupLocationId: pickup,
            dropoffLocationId: dropoff,
            relatedReserveId: null);
        _context.Passengers.Add(passenger);

        ChargeCustomer(subscription, price.Value,
            $"Pasajero frecuente Ida (suscripción {subscription.FrequentSubscriptionId}) reserva {reserve.ReserveId}.",
            reserve.ReserveId);
    }

    private Passenger BuildPassenger(
        FrequentSubscription subscription, Reserve reserve, decimal price,
        int pickupLocationId, int dropoffLocationId, int? relatedReserveId)
    {
        var customer = subscription.Customer;
        return new Passenger
        {
            ReserveId = reserve.ReserveId,
            ReserveRelatedId = relatedReserveId,
            CustomerId = customer.CustomerId,
            FrequentSubscriptionId = subscription.FrequentSubscriptionId,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            DocumentNumber = customer.DocumentNumber,
            Email = customer.Email,
            Phone = string.IsNullOrEmpty(customer.Phone2) ? customer.Phone1 : $"{customer.Phone1} / {customer.Phone2}",
            PickupLocationId = pickupLocationId,
            DropoffLocationId = dropoffLocationId,
            Price = price,
            // PendingPayment (NO Confirmed): el Charge se asienta en cuenta corriente igual,
            // pero el Passenger queda visible para el flow estándar de saldar deuda
            // (GetCustomerPendingReservesAsync filtra por Status = PendingPayment).
            // "Frecuente" = "viaja siempre + le cobramos por adelantado", NO "pre-pagado".
            // Cuando el cliente pague vía POST /pago, el flow normal flipa a Confirmed.
            Status = PassengerStatusEnum.PendingPayment,
            HasTraveled = false
        };
    }

    private void ChargeCustomer(FrequentSubscription subscription, decimal amount, string description, int relatedReserveId)
    {
        var customer = subscription.Customer;
        var transaction = new CustomerAccountTransaction
        {
            CustomerId = customer.CustomerId,
            Date = _dateTimeProvider.UtcNow,
            Type = TransactionType.Charge,
            Amount = amount,
            Description = description,
            RelatedReserveId = relatedReserveId
        };
        _context.CustomerAccountTransactions.Add(transaction);
        customer.CurrentBalance += amount;
        // Marca explícita necesaria: convención del codebase usada en ReserveBusiness y ReservePaymentBusiness.
        // Sin esto, change tracking puede perder el cambio en ciertos contextos (auto-apply post-Create,
        // segundo SaveChangesAsync en la misma transacción, etc.) y CurrentBalance queda desactualizado en DB.
        _context.Customers.Update(customer);
    }

    private async Task<bool> PassengerExists(int reserveId, FrequentSubscription subscription)
    {
        return await _context.Passengers.AnyAsync(p =>
            p.ReserveId == reserveId &&
            p.CustomerId == subscription.CustomerId &&
            p.FrequentSubscriptionId == subscription.FrequentSubscriptionId);
    }

    private async Task<bool> HasCapacity(Reserve reserve)
    {
        var vehicle = await _context.Vehicles
            .Where(v => v.VehicleId == reserve.VehicleId)
            .Select(v => new { v.AvailableQuantity })
            .FirstOrDefaultAsync();
        if (vehicle is null) return false;

        var existing = await _context.Passengers.CountAsync(p =>
            p.ReserveId == reserve.ReserveId &&
            p.Status != PassengerStatusEnum.Cancelled);

        return existing + 1 <= vehicle.AvailableQuantity;
    }

    // TODO: Extraer a `IPricingResolver` cuando aparezca el tercer consumidor (ver ADR 0001).
    // Hoy duplica la lógica de `ReserveBusiness.GetPassengerPriceAsync` deliberadamente para mantener
    // el PR enfocado en el feature de Pasajeros Frecuentes.
    private async Task<decimal?> GetPriceAsync(
        int originId, int destinationId, ReserveTypeIdEnum reserveTypeId, int? dropoffLocationId)
    {
        var trip = await _context.Trips
            .Where(t => t.OriginCityId == originId
                     && t.DestinationCityId == destinationId
                     && t.Status == EntityStatusEnum.Active)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
            .FirstOrDefaultAsync();
        if (trip is null) return null;

        var relevant = trip.Prices.Where(p => p.ReserveTypeId == reserveTypeId).ToList();
        if (relevant.Count == 0) return null;

        int? dropoffCityId = null;
        if (dropoffLocationId.HasValue)
        {
            var directionPrice = relevant.FirstOrDefault(p => p.DirectionId == dropoffLocationId.Value);
            if (directionPrice is not null) return directionPrice.Price;

            var direction = await _context.Directions
                .Where(d => d.DirectionId == dropoffLocationId.Value)
                .Select(d => new { d.CityId })
                .FirstOrDefaultAsync();
            dropoffCityId = direction?.CityId;
        }

        if (dropoffCityId.HasValue)
        {
            var cityPrice = relevant.FirstOrDefault(p => p.CityId == dropoffCityId.Value && p.DirectionId == null);
            if (cityPrice is not null) return cityPrice.Price;
        }

        var basePrice = relevant.FirstOrDefault(p => p.CityId == destinationId && p.DirectionId == null);
        return basePrice?.Price;
    }

    // Lee el ReserveGenerationDays del TenantConfig del tenant actual. Si por alguna razón
    // no existe TenantConfig (deuda histórica, tenant recién creado), cae al default global
    // de IReserveOption como red de seguridad.
    // NOTA: TenantConfig NO implementa ITenantScoped — hay que filtrar explícito por TenantId,
    // el global query filter no aplica acá.
    private async Task<int> GetReserveGenerationDaysAsync()
    {
        var configValue = await _context.TenantConfigs
            .Where(tc => tc.TenantId == _tenantContext.TenantId)
            .Select(tc => (int?)tc.ReserveGenerationDays)
            .FirstOrDefaultAsync();
        return configValue ?? _reserveOption.ReserveGenerationDays;
    }
}
