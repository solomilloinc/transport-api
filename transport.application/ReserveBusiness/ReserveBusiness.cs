using MercadoPago.Client.Common;
using MercadoPago.Client.Payment;
using MercadoPago.Resource.Payment;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Business.ReserveBusiness.Internal;
using Transport.Business.Services.Payment;
using Transport.Domain.CashBoxes;
using Transport.Domain.CashBoxes.Abstraction;
using Transport.Domain.Bookings;
using Transport.Domain.Cities;
using Transport.Domain.Customers;
using Transport.Domain.Customers.Abstraction;
using Transport.Domain.Directions;
using Transport.Domain.Drivers;
using Transport.Domain.Passengers;
using Transport.Domain.Reserves;
using Transport.Domain.Reserves.Abstraction;
using Transport.Domain.Services;
using Transport.Domain.Tenants.Abstraction;
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

    private readonly ReservePassengerItemsEnricher _enricher;
    private readonly ReserveTotalCalculator _totalCalculator;
    private readonly ReservePassengerFactory _passengerFactory;
    private readonly ReservePaymentApplier _paymentApplier;
    private readonly ReservePaymentSummaryReader _paymentSummaryReader;
    private readonly ReservePassengerReportReader _passengerReportReader;
    private readonly ReserveReportReader _reserveReportReader;
    private readonly BookingPaymentStateService _bookingPaymentStateService;
    private readonly ReservePaymentMutationOrchestrator _paymentMutationOrchestrator;
    private readonly ReservePricingResolver _pricingResolver;
    private readonly RoundTripComboPolicy _comboPolicy;
    private readonly ITenantReserveConfigProvider _tenantReserveConfigProvider;

    public ReserveBusiness(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IMercadoPagoPaymentGateway paymentGateway,
        ICustomerBusiness customerBusiness,
        IReserveOption reserveOptions,
        ICashBoxBusiness cashBoxBusiness,
        ITenantReserveConfigProvider tenantReserveConfigProvider,
        ReservePassengerItemsEnricher enricher,
        ReserveTotalCalculator totalCalculator,
        ReservePassengerFactory passengerFactory,
        ReservePaymentApplier paymentApplier,
        ReservePaymentSummaryReader paymentSummaryReader,
        ReservePassengerReportReader passengerReportReader,
        ReserveReportReader reserveReportReader,
        BookingPaymentStateService bookingPaymentStateService,
        ReservePaymentMutationOrchestrator paymentMutationOrchestrator,
        ReservePricingResolver pricingResolver,
        RoundTripComboPolicy comboPolicy)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _paymentGateway = paymentGateway;
        _customerBusiness = customerBusiness;
        _reserveOptions = reserveOptions;
        _cashBoxBusiness = cashBoxBusiness;
        _enricher = enricher;
        _totalCalculator = totalCalculator;
        _passengerFactory = passengerFactory;
        _paymentApplier = paymentApplier;
        _paymentSummaryReader = paymentSummaryReader;
        _passengerReportReader = passengerReportReader;
        _reserveReportReader = reserveReportReader;
        _bookingPaymentStateService = bookingPaymentStateService;
        _paymentMutationOrchestrator = paymentMutationOrchestrator;
        _pricingResolver = pricingResolver;
        _comboPolicy = comboPolicy;
        _tenantReserveConfigProvider = tenantReserveConfigProvider;
    }

    /// <summary>
    /// Construye el grafo de colaboradores como antes del registro en DI (tests y llamadas directas).
    /// </summary>
    public static ReserveBusiness CreateWithDefaultCollaborators(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IMercadoPagoPaymentGateway paymentGateway,
        ICustomerBusiness customerBusiness,
        IReserveOption reserveOptions,
        ICashBoxBusiness cashBoxBusiness,
        ITenantReserveConfigProvider tenantReserveConfigProvider)
    {
        var bookingPaymentStateService = new BookingPaymentStateService(context);
        return new ReserveBusiness(
            context,
            unitOfWork,
            userContext,
            paymentGateway,
            customerBusiness,
            reserveOptions,
            cashBoxBusiness,
            tenantReserveConfigProvider,
            new ReservePassengerItemsEnricher(context, tenantReserveConfigProvider),
            new ReserveTotalCalculator(),
            new ReservePassengerFactory(),
            new ReservePaymentApplier(context, cashBoxBusiness, paymentGateway),
            new ReservePaymentSummaryReader(context),
            new ReservePassengerReportReader(context),
            new ReserveReportReader(context),
            bookingPaymentStateService,
            new ReservePaymentMutationOrchestrator(context, cashBoxBusiness, bookingPaymentStateService),
            new ReservePricingResolver(context, tenantReserveConfigProvider),
            new RoundTripComboPolicy());
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

    public async Task<Result<bool>> CreatePassengerReserves(PassengerReserveCreateRequestWrapperDto passengerReserves)
    {
        var customerId = passengerReserves.Items.FirstOrDefault()?.CustomerId;
        if (customerId is null || customerId == 0)
            return Result.Failure<bool>(Error.Validation(
                "PassengerReserveCreateRequestWrapperDto.CustomerIdRequired",
                "CustomerId is required in at least one passenger item."));

        var payer = await _context.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId);
        if (payer is null)
            return Result.Failure<bool>(CustomerError.NotFound);

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var enrichedResult = await _enricher.EnrichForAdminAsync(passengerReserves.Items);
            if (enrichedResult.IsFailure)
                return Result.Failure<bool>(enrichedResult.Error);

            var enriched = enrichedResult.Value;

            // Admin: el front manda el precio y debe coincidir con el que el server resuelve.
            foreach (var item in enriched)
            {
                if (item.AdminDto!.Price != item.ResolvedPrice)
                    return Result.Failure<bool>(ReserveError.PriceNotAvailable);
            }

            var totalExpected = _totalCalculator.Compute(enriched);
            var mainReserveId = _comboPolicy.ResolveMainReserveId(
                passengerReserves.Items.Select(x => x.ReserveId));
            var bookingStatus = ResolveAdminBookingStatus(
                passengerReserves.Payments,
                totalExpected);

            var booking = CreateBookingMirrorIfAvailable(
                customerId: payer.CustomerId,
                bookingStatus: bookingStatus,
                enriched: enriched);

            foreach (var item in enriched)
            {
                var passenger = _passengerFactory.BuildAdmin(item, payer);
                item.Reserve.Passengers.Add(passenger);
                _context.Passengers.Add(passenger);
            }

            await _context.SaveChangesWithOutboxAsync();

            var paymentResult = await _paymentApplier.ApplyAdminAsync(
                passengerReserves, enriched, payer, totalExpected, mainReserveId, booking?.BookingId);
            if (paymentResult.IsFailure)
                return paymentResult;

            if (booking?.BookingId is int adminBookingId)
                await _bookingPaymentStateService.UpdateBookingStatusFromPaymentsAsync(adminBookingId);

            return paymentResult;
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
        var distinctReserveIds = dto.Items.Select(i => i.ReserveId).Distinct().ToList();
        var reserveDatesById = await _context.Reserves
            .AsNoTracking()
            .Where(r => distinctReserveIds.Contains(r.ReserveId))
            .ToDictionaryAsync(r => r.ReserveId, r => r.ReserveDate);

        var tenantCfg = await _tenantReserveConfigProvider.GetCurrentAsync();
        var validationResult = ReservePassengerItemsValidator.ValidateUserReserveCombination(
            dto.Items,
            reserveDatesById,
            tenantCfg.RoundTripSameDayOnly);
        if (validationResult.IsFailure)
            return Result.Failure<CreateReserveExternalResult>(validationResult.Error);

        var enrichedResult = await _enricher.EnrichForExternalAsync(dto.Items);
        if (enrichedResult.IsFailure)
            return Result.Failure<CreateReserveExternalResult>(enrichedResult.Error);

        var enriched = enrichedResult.Value;
        var totalExpected = _totalCalculator.Compute(enriched);
        var hasExternalPayment = dto.Payment is not null;
        var bookingStatus = hasExternalPayment
            ? BookingStatusEnum.Confirmed
            : BookingStatusEnum.PendingPayment;

        var booking = CreateBookingMirrorIfAvailable(
            customerId: dto.Items.FirstOrDefault()?.CustomerId,
            bookingStatus: bookingStatus,
            enriched: enriched);

        var reserves = new List<Reserve>();
        foreach (var item in enriched)
        {
            var passenger = _passengerFactory.BuildExternal(item, hasExternalPayment);
            item.Reserve.Passengers.Add(passenger);
            _context.Passengers.Add(passenger);

            if (!reserves.Any(r => r.ReserveId == item.ReserveId))
                reserves.Add(item.Reserve);
        }

        await _context.SaveChangesWithOutboxAsync();

        if (dto.Payment is null)
            return await CompleteExternalReserveWithoutTokenPaymentAsync(
                dto, enriched, reserves, totalExpected, booking);

        if (totalExpected != dto.Payment.TransactionAmount)
            return Result.Failure<CreateReserveExternalResult>(
                ReserveError.InvalidPaymentAmount(totalExpected, dto.Payment.TransactionAmount));

        return await CompleteExternalReserveWithTokenPaymentAsync(dto, reserves, totalExpected, booking);
    }

    private async Task<Result<CreateReserveExternalResult>> CompleteExternalReserveWithoutTokenPaymentAsync(
        PassengerReserveCreateRequestWrapperExternalDto dto,
        IReadOnlyList<EnrichedPassengerItem> enriched,
        List<Reserve> reserves,
        decimal totalExpected,
        Booking? booking)
    {
        var mpItems = _totalCalculator.BuildMpItems(enriched);
        var preferenceResult = await _paymentApplier.ApplyExternalPendingAsync(
            totalExpected, reserves, dto.Items.First(), mpItems, booking?.BookingId);

        if (preferenceResult.IsFailure)
            return Result.Failure<CreateReserveExternalResult>(preferenceResult.Error);

        if (booking?.BookingId is int pendingBookingId)
            await _bookingPaymentStateService.UpdateBookingStatusFromPaymentsAsync(pendingBookingId);

        return Result.Success(new CreateReserveExternalResult(PaymentStatus.Pending, preferenceResult.Value));
    }

    private async Task<Result<CreateReserveExternalResult>> CompleteExternalReserveWithTokenPaymentAsync(
        PassengerReserveCreateRequestWrapperExternalDto dto,
        List<Reserve> reserves,
        decimal totalExpected,
        Booking? booking)
    {
        var paymentResult = await _paymentApplier.ApplyExternalWithTokenAsync(
            dto.Payment!, reserves, booking?.BookingId);
        if (paymentResult.IsFailure)
            return Result.Failure<CreateReserveExternalResult>(paymentResult.Error);

        if (booking?.BookingId is int tokenBookingId)
            await _bookingPaymentStateService.UpdateBookingStatusFromPaymentsAsync(tokenBookingId);

        var mainReserve = reserves.OrderBy(r => r.ReserveId).First();
        var firstPassenger = dto.Items.First();
        mainReserve.Raise(new CustomerReserveCreatedEvent(
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
            TotalPrice: totalExpected
        ));

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(new CreateReserveExternalResult(PaymentStatus.Approved, null));
    }

    private BookingStatusEnum ResolveAdminBookingStatus(
        IReadOnlyCollection<CreatePaymentRequestDto> payments,
        decimal totalExpected)
    {
        if (payments.Count == 0)
            return BookingStatusEnum.PendingPayment;

        var provided = payments.Sum(p => p.TransactionAmount);
        return provided >= totalExpected
            ? BookingStatusEnum.Confirmed
            : BookingStatusEnum.PendingPayment;
    }

    private Booking? CreateBookingMirrorIfAvailable(
        int? customerId,
        BookingStatusEnum bookingStatus,
        IReadOnlyList<EnrichedPassengerItem> enriched)
    {
        if (enriched.Count == 0)
            return null;

        var reserveIds = enriched
            .Select(x => x.Reserve.ReserveId)
            .Distinct()
            .ToList();

        var booking = new Booking
        {
            CustomerId = customerId,
            Type = reserveIds.Count > 1 ? BookingTypeEnum.RoundTrip : BookingTypeEnum.OneWay,
            Status = bookingStatus,
            TotalAmount = 0m,
        };

        _context.Bookings.Add(booking);

        var mainReserveId = _comboPolicy.ResolveMainReserveId(reserveIds);

        foreach (var group in enriched.GroupBy(x => x.Reserve.ReserveId))
        {
            var legSubtotal = _totalCalculator.ComputeLegSubtotal(group);
            var item = group.First();
            var isPrincipalLeg = item.ReserveId == mainReserveId;
            var direction = reserveIds.Count == 1 || isPrincipalLeg
                ? LegDirectionEnum.Outbound
                : LegDirectionEnum.Return;

            var leg = new BookingLeg
            {
                Booking = booking,
                ReserveId = item.ReserveId,
                Direction = direction,
                AppliedReserveTypeId = item.AppliedReserveType,
                LegSubtotal = legSubtotal,
            };

            booking.Legs.Add(leg);
            _context.BookingLegs.Add(leg);
        }

        booking.SyncTotalAmountFromLegs();
        return booking;
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

            var internalStatus = ExternalPaymentStatusMapper.Map(mpPayment.Status);
            if (internalStatus == null)
                return Result.Failure<bool>(Error.Validation("Payment.InvalidStatus",
                    "Estado de pago no reconocido"));

            await ApplyExternalPaymentResultAsync(
                parentPayment,
                internalStatus.Value,
                mpPayment.StatusDetail,
                mpPayment.Id,
                JsonConvert.SerializeObject(mpPayment));

            await _context.SaveChangesWithOutboxAsync();
            if (parentPayment.BookingId is int reserveBookingId)
                await _bookingPaymentStateService.UpdateBookingStatusFromPaymentsAsync(reserveBookingId);
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

            var internalStatus = ExternalPaymentStatusMapper.Map(externalPayment.Status);
            if (internalStatus == null)
                return Result.Failure<bool>(Error.Validation("Payment.InvalidStatus",
                    "Estado de pago no reconocido"));

            await ApplyExternalPaymentResultAsync(
                parentPayment,
                internalStatus.Value,
                externalPayment.StatusDetail,
                externalPayment.PaymentExternalId,
                externalPayment.RawJson);

            await _context.SaveChangesWithOutboxAsync();
            if (parentPayment.BookingId is int webhookBookingId)
                await _bookingPaymentStateService.UpdateBookingStatusFromPaymentsAsync(webhookBookingId);
            return Result.Success(true);
        });
    }

    private async Task ApplyExternalPaymentResultAsync(
        ReservePayment parentPayment,
        StatusPaymentEnum internalStatus,
        string statusDetail,
        long? paymentExternalId,
        string rawJson)
    {
        parentPayment.Status = internalStatus;
        parentPayment.StatusDetail = statusDetail;
        parentPayment.PaymentExternalId = paymentExternalId;
        parentPayment.ResultApiExternalRawJson = rawJson;
        parentPayment.UpdatedBy = "System";
        parentPayment.UpdatedDate = DateTime.UtcNow;
        _context.ReservePayments.Update(parentPayment);

        var children = await _context.ReservePayments
            .Where(c => c.ParentReservePaymentId == parentPayment.ReservePaymentId)
            .ToListAsync();

        foreach (var child in children)
        {
            child.Status = parentPayment.Status;
            child.StatusDetail = parentPayment.StatusDetail;
            _context.ReservePayments.Update(child);
        }

        var reservesToUpdate = await LoadReservesForParentPaymentOutcomeAsync(parentPayment, children);

        PassengerPaymentStatusApplier.ApplyWebhookParentPaymentOutcome(
            reservesToUpdate,
            internalStatus,
            _context);
    }

    /// <summary>
    /// Reservas cuyos pasajeros deben reflejar el estado final del pago padre (webhook / sync por id MP).
    /// Si el pago es de booking, incluye todas las patas del espejo; si no, la reserva anclada en el pago y filas hijas.
    /// </summary>
    private async Task<List<Reserve>> LoadReservesForParentPaymentOutcomeAsync(
        ReservePayment parentPayment,
        IReadOnlyList<ReservePayment> childPayments)
    {
        if (parentPayment.BookingId is int bookingId)
        {
            var reserveIds = await _context.BookingLegs
                .Where(bl => bl.BookingId == bookingId)
                .Select(bl => bl.ReserveId)
                .Distinct()
                .ToListAsync();

            if (reserveIds.Count > 0)
            {
                return await _context.Reserves
                    .Include(r => r.Passengers)
                    .Where(r => reserveIds.Contains(r.ReserveId))
                    .ToListAsync();
            }
        }

        var reserveIdsToTouch = new List<int> { parentPayment.ReserveId };
        reserveIdsToTouch.AddRange(childPayments.Select(ch => ch.ReserveId));

        return await _context.Reserves
            .Include(r => r.Passengers)
            .Where(r => reserveIdsToTouch.Distinct().Contains(r.ReserveId))
            .ToListAsync();
    }

    // Métodos de reportes actualizados
    public async Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>> GetReserveReport(
        DateTime reserveDate, PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto)
    {
        return await _reserveReportReader.GetReserveReportAsync(reserveDate, requestDto);
    }


    public async Task<Result<ReserveGroupedPagedReportResponseDto>> GetReserveReport(
        PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto)
    {
        return await _reserveReportReader.GetGroupedReserveReportAsync(requestDto);
    }

    public async Task<Result<PagedReportResponseDto<PassengerReserveReportResponseDto>>> GetReservePassengerReport(
        int reserveId,
        PagedReportRequestDto<PassengerReserveReportFilterRequestDto> requestDto)
    {
        return await _passengerReportReader.GetReservePassengerReportAsync(reserveId, requestDto);
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
            return await _paymentMutationOrchestrator.CreatePaymentsAsync(customerId, reserveId, payments);
        });
    }

    public async Task<Result<bool>> CreateBookingPaymentsAsync(
      int customerId,
      int bookingId,
      List<CreatePaymentRequestDto> payments)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            return await _paymentMutationOrchestrator.CreateBookingPaymentsAsync(customerId, bookingId, payments);
        });
    }

    public async Task<Result<bool>> SettleCustomerDebtAsync(SettleCustomerDebtRequestDto request)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            return await _paymentMutationOrchestrator.SettleCustomerDebtAsync(request);
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

            var totalPaid = await ReservePaidParentPaymentsQueries.SumPaidParentsAttributedToReserveForCustomerAsync(
                _context,
                reserve.ReserveId,
                customerId);

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

            // SlotsLocked = pasajeros bloqueados (LockReserveSlots.PassengerCount). Cada pasajero genera
            // un ítem por pata (ida + vuelta ⇒ 2 ítems por pasajero), no un ítem por lock.
            // ReturnReserveId puede venir como 0 desde datos viejos; tratarlo como "sin vuelta".
            var legCount = slotLock.ReturnReserveId is > 0 ? 2 : 1;
            var expectedItemCount = slotLock.SlotsLocked * legCount;
            if (request.Items.Count != expectedItemCount)
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
        return await _paymentSummaryReader.GetReservePaymentSummaryAsync(reserveId, requestDto);
    }

    public async Task<Result<PagedReportResponseDto<BookingPaymentSummaryResponseDto>>> GetBookingPaymentSummary(
        int bookingId,
        PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto> requestDto)
    {
        return await _paymentSummaryReader.GetBookingPaymentSummaryAsync(bookingId, requestDto);
    }

    /// <summary>
    /// Calculates totals for a (potential) reserve cart applying the same pricing
    /// rules used at creation time, including the per-tenant
    /// "IdaVuelta combo only on the same day" rule. The frontend should call this
    /// before checkout so it can show the user the price they will actually pay
    /// and explain any discounts that were lost.
    /// </summary>
    public async Task<Result<ReserveQuoteResponseDto>> QuoteAsync(ReserveQuoteRequestDto request)
    {
        if (request?.Items is null || request.Items.Count == 0)
            return Result.Failure<ReserveQuoteResponseDto>(Error.Validation(
                "ReserveQuote.NoItems",
                "At least one item is required."));

        // Pre-load all referenced trips with their prices and origin/destination ids.
        var tripIds = request.Items.Select(i => i.TripId).Distinct().ToList();
        var trips = await _context.Trips
            .Where(t => tripIds.Contains(t.TripId) && t.Status == EntityStatusEnum.Active)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
            .ToDictionaryAsync(t => t.TripId);

        var responseItems = new List<ReserveQuoteResponseItemDto>(request.Items.Count);
        decimal total = 0m;
        var discountsLost = new List<DiscountLostDto>();
        var lostCodes = new HashSet<string>();
        var mainItem = request.Items.First();

        foreach (var item in request.Items)
        {
            if (!trips.TryGetValue(item.TripId, out var trip))
                return Result.Failure<ReserveQuoteResponseDto>(TripError.TripNotFound);

            var requestedType = (ReserveTypeIdEnum)item.ReserveTypeId;

            // Find the related item in the same cart (the other half of the combo, if any).
            var relatedItem = request.Items.FirstOrDefault(other => !ReferenceEquals(other, item) && other.TripId != item.TripId);
            DateTime? relatedDate = relatedItem?.ReserveDate;

            var appliedType = await _pricingResolver.ResolveAppliedReserveTypeAsync(requestedType, item.ReserveDate, relatedDate);

            var (unitPrice, _) = await _pricingResolver.GetPassengerPriceAsync(
                trip.OriginCityId,
                trip.DestinationCityId,
                appliedType,
                item.DropoffLocationId);

            if (unitPrice is null)
                return Result.Failure<ReserveQuoteResponseDto>(ReserveError.PriceNotAvailable);

            var passengerCount = item.PassengerCount <= 0 ? 1 : item.PassengerCount;
            var isPrincipalLeg = ReferenceEquals(item, mainItem);
            var isComboReturnLeg = request.Items.Count == 2
                && _comboPolicy.IsComboReturnLeg(appliedType, isPrincipalLeg);
            var subtotal = _comboPolicy.ComputeSubtotal(unitPrice.Value, passengerCount, isComboReturnLeg);
            total += subtotal;

            QuoteReasonEnum? reason = null;
            if (appliedType != requestedType && requestedType == ReserveTypeIdEnum.IdaVuelta)
            {
                reason = QuoteReasonEnum.RoundTripDifferentDay;
                if (lostCodes.Add("RoundTripSameDayOnly"))
                {
                    discountsLost.Add(new DiscountLostDto(
                        "RoundTripSameDayOnly",
                        "El precio combo ida y vuelta aplica sólo el mismo día."));
                }
            }

            responseItems.Add(new ReserveQuoteResponseItemDto(
                item.TripId,
                (int)requestedType,
                (int)appliedType,
                unitPrice.Value,
                subtotal,
                reason));
        }

        return new ReserveQuoteResponseDto(responseItems, total, discountsLost);
    }

}