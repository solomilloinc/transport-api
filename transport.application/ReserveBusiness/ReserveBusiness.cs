using MercadoPago.Client.Common;
using MercadoPago.Client.Payment;
using MercadoPago.Resource.Payment;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq;
using System.Linq.Expressions;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Business.ReserveBusiness.Internal;
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
    private readonly ITenantReserveConfigProvider _tenantReserveConfigProvider;

    // Colaboradores internos para la creación de reservas con pasajeros.
    // Se instancian acá (no por DI) para no cambiar la firma del constructor
    // y mantener intactos los wirings de tests existentes.
    private readonly ReservePassengerItemsEnricher _enricher;
    private readonly ReserveTotalCalculator _totalCalculator;
    private readonly ReservePassengerFactory _passengerFactory;
    private readonly ReservePaymentApplier _paymentApplier;

    public ReserveBusiness(IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IMercadoPagoPaymentGateway paymentGateway,
        ICustomerBusiness customerBusiness,
        IReserveOption reserveOptions,
        ICashBoxBusiness cashBoxBusiness,
        ITenantReserveConfigProvider tenantReserveConfigProvider)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _paymentGateway = paymentGateway;
        _customerBusiness = customerBusiness;
        _reserveOptions = reserveOptions;
        _cashBoxBusiness = cashBoxBusiness;
        _tenantReserveConfigProvider = tenantReserveConfigProvider;

        _enricher = new ReservePassengerItemsEnricher(context, tenantReserveConfigProvider);
        _totalCalculator = new ReserveTotalCalculator();
        _passengerFactory = new ReservePassengerFactory();
        _paymentApplier = new ReservePaymentApplier(context, cashBoxBusiness, paymentGateway);
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
            var mainReserveId = MainReserveSelector.ByMinReserveId(passengerReserves.Items);

            foreach (var item in enriched)
            {
                var passenger = _passengerFactory.BuildAdmin(item, payer);
                item.Reserve.Passengers.Add(passenger);
                _context.Passengers.Add(passenger);
            }

            await _context.SaveChangesWithOutboxAsync();

            return await _paymentApplier.ApplyAdminAsync(
                passengerReserves, enriched, payer, totalExpected, mainReserveId);
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
        var validationResult = ReservePassengerItemsValidator.ValidateUserReserveCombination(dto.Items);
        if (validationResult.IsFailure)
            return Result.Failure<CreateReserveExternalResult>(validationResult.Error);

        var enrichedResult = await _enricher.EnrichForExternalAsync(dto.Items);
        if (enrichedResult.IsFailure)
            return Result.Failure<CreateReserveExternalResult>(enrichedResult.Error);

        var enriched = enrichedResult.Value;
        var totalExpected = _totalCalculator.Compute(enriched);
        var hasExternalPayment = dto.Payment is not null;

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
        {
            var mpItems = _totalCalculator.BuildMpItems(enriched);
            var preferenceResult = await _paymentApplier.ApplyExternalPendingAsync(
                totalExpected, reserves, dto.Items.First(), mpItems);

            if (preferenceResult.IsFailure)
                return Result.Failure<CreateReserveExternalResult>(preferenceResult.Error);

            return Result.Success(new CreateReserveExternalResult(PaymentStatus.Pending, preferenceResult.Value));
        }

        if (totalExpected != dto.Payment.TransactionAmount)
            return Result.Failure<CreateReserveExternalResult>(
                ReserveError.InvalidPaymentAmount(totalExpected, dto.Payment.TransactionAmount));

        var paymentResult = await _paymentApplier.ApplyExternalWithTokenAsync(dto.Payment, reserves);
        if (paymentResult.IsFailure)
            return Result.Failure<CreateReserveExternalResult>(paymentResult.Error);

        // Evento de confirmación: se levanta acá (no en el applier) porque es decisión
        // del flujo, no del pago. La reserva principal por ReserveId menor coincide con
        // el comportamiento histórico cuando se elegía OrderBy(ReserveId).First().
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

        // Load TripPickupStops for pickup time calculation
        var pickupDirectionId = requestDto.Filters.PickupDirectionId;
        List<TripPickupStop>? idaTripPickupStops = null;
        if (pickupDirectionId.HasValue)
        {
            idaTripPickupStops = await _context.TripPickupStops
                .Where(td => td.TripId == tripId && td.Status == EntityStatusEnum.Active)
                .Include(td => td.Direction)
                .OrderBy(td => td.Order)
                .ToListAsync();

            if (!idaTripPickupStops.Any(td => td.DirectionId == pickupDirectionId.Value))
                return Result.Failure<ReserveGroupedPagedReportResponseDto>(TripError.TripPickupStopNotFound);
        }

        // Fetch the return trip (inverse route) if needed
        Trip? vueltaTrip = null;
        List<TripPickupStop>? vueltaTripPickupStops = null;
        if (vueltaDate.HasValue)
        {
            vueltaTrip = await _context.Trips
                .Where(t => t.OriginCityId == destinationId && t.DestinationCityId == originId
                         && t.Status == EntityStatusEnum.Active)
                .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
                .FirstOrDefaultAsync();

            if (vueltaTrip != null && pickupDirectionId.HasValue)
            {
                vueltaTripPickupStops = await _context.TripPickupStops
                    .Where(td => td.TripId == vueltaTrip.TripId && td.Status == EntityStatusEnum.Active)
                    .Include(td => td.Direction)
                    .OrderBy(td => td.Order)
                    .ToListAsync();
            }
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
                var stopSchedules = idaTripPickupStops?.Select(td => new ReserveStopScheduleDto(
                    td.DirectionId,
                    td.Direction.Name,
                    td.Order,
                    rp.DepartureHour.Add(td.PickupTimeOffset).ToString(@"hh\:mm")
                )).ToList();
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
                    rp.Vehicle.InternalNumber,
                    rp.TripId,
                    stopSchedules
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
                    var vueltaStopSchedules = vueltaTripPickupStops?.Select(td => new ReserveStopScheduleDto(
                        td.DirectionId,
                        td.Direction.Name,
                        td.Order,
                        rp.DepartureHour.Add(td.PickupTimeOffset).ToString(@"hh\:mm")
                    )).ToList();
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
                        rp.Vehicle.InternalNumber,
                        rp.TripId,
                        vueltaStopSchedules
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
                p.HasTraveled,
                (int)p.Status);
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

        foreach (var item in request.Items)
        {
            if (!trips.TryGetValue(item.TripId, out var trip))
                return Result.Failure<ReserveQuoteResponseDto>(TripError.TripNotFound);

            var requestedType = (ReserveTypeIdEnum)item.ReserveTypeId;

            // Find the related item in the same cart (the other half of the combo, if any).
            var relatedItem = request.Items.FirstOrDefault(other => !ReferenceEquals(other, item) && other.TripId != item.TripId);
            DateTime? relatedDate = relatedItem?.ReserveDate;

            var appliedType = await ResolveAppliedReserveTypeAsync(requestedType, item.ReserveDate, relatedDate);

            var (unitPrice, _) = await GetPassengerPriceAsync(
                trip.OriginCityId,
                trip.DestinationCityId,
                appliedType,
                item.DropoffLocationId);

            if (unitPrice is null)
                return Result.Failure<ReserveQuoteResponseDto>(ReserveError.PriceNotAvailable);

            var passengerCount = item.PassengerCount <= 0 ? 1 : item.PassengerCount;
            var subtotal = unitPrice.Value * passengerCount;
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

    /// <summary>
    /// Resolves which <see cref="ReserveTypeIdEnum"/> should actually be charged
    /// given the requested type and the dates of the leg being priced and its
    /// related leg (if any). Encapsulates the "round-trip combo only on the same day"
    /// business rule controlled by <see cref="Domain.Tenants.TenantReserveConfig.RoundTripSameDayOnly"/>.
    /// </summary>
    /// <param name="requestedType">The type the caller asked for (Ida or IdaVuelta).</param>
    /// <param name="currentReserveDate">Date of the leg being priced.</param>
    /// <param name="relatedReserveDate">
    /// Date of the related leg (the other half of the IdaVuelta combo).
    /// Null when there is no related leg in the cart, in which case an
    /// IdaVuelta request degrades to Ida.
    /// </param>
    private async Task<ReserveTypeIdEnum> ResolveAppliedReserveTypeAsync(
        ReserveTypeIdEnum requestedType,
        DateTime currentReserveDate,
        DateTime? relatedReserveDate)
    {
        if (requestedType != ReserveTypeIdEnum.IdaVuelta)
            return requestedType;

        var config = await _tenantReserveConfigProvider.GetCurrentAsync();
        if (!config.RoundTripSameDayOnly)
            return ReserveTypeIdEnum.IdaVuelta;

        if (!relatedReserveDate.HasValue)
            return ReserveTypeIdEnum.Ida;

        return relatedReserveDate.Value.Date == currentReserveDate.Date
            ? ReserveTypeIdEnum.IdaVuelta
            : ReserveTypeIdEnum.Ida;
    }

    private async Task<(decimal? Price, ReserveTypeIdEnum AppliedType)> GetPassengerPriceAsync(
        int originId,
        int destinationId,
        ReserveTypeIdEnum reserveTypeId,
        int? dropoffLocationId,
        DateTime? currentReserveDate = null,
        DateTime? relatedReserveDate = null)
    {
        // Apply the "round-trip same day only" business rule, if applicable.
        // When called without dates (legacy callers), the rule is skipped.
        var appliedType = currentReserveDate.HasValue
            ? await ResolveAppliedReserveTypeAsync(reserveTypeId, currentReserveDate.Value, relatedReserveDate)
            : reserveTypeId;

        // 1. Find the trip for this origin/destination
        var trip = await _context.Trips
            .Where(t => t.OriginCityId == originId
                     && t.DestinationCityId == destinationId
                     && t.Status == EntityStatusEnum.Active)
            .Include(t => t.Prices.Where(p => p.Status == EntityStatusEnum.Active))
            .FirstOrDefaultAsync();

        if (trip is null)
            return (null, appliedType);

        // 2. Filter prices for the applied reserve type (OneWay/RoundTrip)
        var relevantPrices = trip.Prices
            .Where(p => p.ReserveTypeId == appliedType)
            .ToList();

        if (!relevantPrices.Any())
            return (null, appliedType);

        // 3. Determine the Dropoff City ID if a specific location is provided
        int? dropoffCityId = null;
        if (dropoffLocationId.HasValue)
        {
            var dropoffDirection = await _context.Directions.Where(x => x.DirectionId == dropoffLocationId.Value).FirstOrDefaultAsync();
            dropoffCityId = dropoffDirection?.CityId;

            // PRIORITY 1: Specific Price for this Direction (Stop)
            var directionPrice = relevantPrices.FirstOrDefault(p => p.DirectionId == dropoffLocationId.Value);
            if (directionPrice != null)
                return (directionPrice.Price, appliedType);
        }

        // PRIORITY 2: Price for the Dropoff City (intermediate city)
        if (dropoffCityId.HasValue)
        {
            var cityPrice = relevantPrices.FirstOrDefault(p => p.CityId == dropoffCityId.Value && p.DirectionId == null);
            if (cityPrice != null)
                return (cityPrice.Price, appliedType);
        }

        // PRIORITY 3: Base Price (Destination City)
        // This is the fallback if no intermediate price is found
        var basePrice = relevantPrices
            .Where(p => p.CityId == destinationId && p.DirectionId == null)
            .FirstOrDefault();

        return (basePrice?.Price, appliedType);
    }
}