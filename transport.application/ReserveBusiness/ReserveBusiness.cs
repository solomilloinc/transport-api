using MercadoPago.Client.Common;
using MercadoPago.Client.Payment;
using MercadoPago.Resource.Payment;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq.Expressions;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Business.Services.Payment;
using Transport.Domain.Customers;
using Transport.Domain.Customers.Abstraction;
using Transport.Domain.Directions;
using Transport.Domain.Drivers;
using Transport.Domain.Reserves;
using Transport.Domain.Reserves.Abstraction;
using Transport.Domain.Services;
using Transport.Domain.Users;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Customer;
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

    public async Task<Result<bool>> CreatePassengerReserves(CustomerReserveCreateRequestWrapperDto customerReserves)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            decimal totalExpectedAmount = 0m;
            Customer? customer = null;

            var mainReserveId = customerReserves.Items.Min(i => i.ReserveId);

            var servicesCache = new Dictionary<int, Service>();

            var reserveMap = new Dictionary<int, Reserve>();

            foreach (var passenger in customerReserves.Items)
            {
                var reserve = await _context.Reserves
                    .Include(r => r.CustomerReserves)
                    .Include(r => r.Driver)
                    .SingleOrDefaultAsync(r => r.ReserveId == passenger.ReserveId);

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

                    servicesCache[reserve.ServiceId] = service;
                }

                var vehicle = await _context.Vehicles.FindAsync(reserve.VehicleId);

                var existingPassengerCount = reserve.CustomerReserves.Count;
                var totalAfterInsert = existingPassengerCount + customerReserves.Items.Count;

                if (totalAfterInsert > vehicle.AvailableQuantity)
                    return Result.Failure<bool>(ReserveError.VehicleQuantityNotAvailable(existingPassengerCount, customerReserves.Items.Count, vehicle.AvailableQuantity));

                var reservePrice = service.ReservePrices.SingleOrDefault(p => p.ReserveTypeId == (ReserveTypeIdEnum)passenger.ReserveTypeId);
                if (reservePrice is null || passenger.Price != reservePrice.Price)
                    return Result.Failure<bool>(ReserveError.PriceNotAvailable);

                var customerResult = await _customerBusiness.GetOrCreateFromPassengerAsync(passenger);
                if (!customerResult.IsSuccess)
                    return Result.Failure<bool>(customerResult.Error);

                customer = customerResult.Value;

                if (reserve.CustomerReserves.Any(cr => cr.CustomerId == customer.CustomerId))
                    return Result.Failure<bool>(ReserveError.CustomerAlreadyExists(customer.DocumentNumber));

                var pickupResult = await GetDirectionAsync(passenger.PickupLocationId, "Pickup");
                if (pickupResult.IsFailure) return Result.Failure<bool>(pickupResult.Error);

                var dropoffResult = await GetDirectionAsync(passenger.DropoffLocationId, "Dropoff");
                if (dropoffResult.IsFailure) return Result.Failure<bool>(dropoffResult.Error);

                var newCustomerReserve = new CustomerReserve
                {
                    Customer = customer,
                    ReserveId = reserve.ReserveId,
                    DropoffLocationId = passenger.DropoffLocationId,
                    PickupLocationId = passenger.PickupLocationId,
                    HasTraveled = passenger.HasTraveled,
                    Price = reservePrice.Price,
                    IsPayment = passenger.IsPayment,
                    CustomerFullName = $"{customer.FirstName} {customer.LastName}",
                    DestinationCityName = service.Destination.Name,
                    OriginCityName = service.Origin.Name,
                    DriverName = reserve.Driver != null ? $"{reserve.Driver.FirstName} {reserve.Driver.LastName}" : null,
                    PickupAddress = pickupResult.Value?.Name,
                    DropoffAddress = dropoffResult.Value?.Name,
                    ServiceName = service.Name,
                    VehicleInternalNumber = vehicle.InternalNumber,
                    CustomerEmail = customer.Email,
                    DocumentNumber = customer.DocumentNumber,
                    Phone1 = customer.Phone1,
                    Phone2 = customer.Phone2,
                    Status = CustomerReserveStatusEnum.Confirmed
                };

                reserve.CustomerReserves.Add(newCustomerReserve);
                reserve.Raise(new CustomerReserveCreatedEvent(newCustomerReserve.CustomerReserveId));
                _context.Reserves.Update(reserve);

                totalExpectedAmount += reservePrice.Price;
            }

            var reserveIds = customerReserves.Items.Select(i => i.ReserveId).Distinct().ToList();

            string BuildDescription()
            {
                if (reserveIds.Count == 1)
                {
                    var rid = reserveIds[0];
                    var reserve = reserveMap[rid];
                    var service = servicesCache[reserve.ServiceId];
                    var type = customerReserves.Items.First(i => i.ReserveId == rid).ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta
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

            var description = BuildDescription();

            var chargeTransaction = new CustomerAccountTransaction
            {
                CustomerId = customer!.CustomerId,
                Date = DateTime.UtcNow,
                Type = TransactionType.Charge,
                Amount = totalExpectedAmount,
                Description = description,
                RelatedReserveId = mainReserveId
            };
            _context.CustomerAccountTransactions.Add(chargeTransaction);

            if (!customerReserves.Payments.Any())
            {
                customer.CurrentBalance += totalExpectedAmount;
                _context.Customers.Update(customer);
                await _context.SaveChangesWithOutboxAsync();
                return Result.Success(true);
            }

            var totalProvidedAmount = customerReserves.Payments.Sum(p => p.TransactionAmount);
            //if (totalExpectedAmount != totalProvidedAmount)
            //    return Result.Failure<bool>(ReserveError.InvalidPaymentAmount(totalExpectedAmount, totalProvidedAmount));

            var parentPaymentDto = customerReserves.Payments.First();

            var parentPayment = new ReservePayment
            {
                ReserveId = customerReserves.Items.First().ReserveId,
                CustomerId = customer.CustomerId,
                Amount = parentPaymentDto.TransactionAmount,
                Method = (PaymentMethodEnum)parentPaymentDto.PaymentMethod,
                Status = StatusPaymentEnum.Paid
            };

            _context.ReservePayments.Add(parentPayment);
            await _context.SaveChangesWithOutboxAsync();

            var parentId = parentPayment.ReservePaymentId;

            var remainingPayments = customerReserves.Payments.Skip(1).ToList();

            for (int i = 0; i < remainingPayments.Count; i++)
            {
                var paymentDto = remainingPayments[i];
                var reserveId = reserveIds.ElementAtOrDefault(i + 1) != 0 ? reserveIds.ElementAtOrDefault(i + 1) : reserveIds.First();

                var childPayment = new ReservePayment
                {
                    ReserveId = reserveId,
                    CustomerId = customer.CustomerId,
                    Amount = 0,
                    Method = (PaymentMethodEnum)paymentDto.PaymentMethod,
                    Status = StatusPaymentEnum.Paid,
                    ParentReservePaymentId = parentId
                };

                _context.ReservePayments.Add(childPayment);
            }

            var paymentTransaction = new CustomerAccountTransaction
            {
                CustomerId = customer.CustomerId,
                Date = DateTime.UtcNow,
                Type = TransactionType.Payment,
                Amount = -totalProvidedAmount,
                Description = $"Pago aplicado a {description}",
                RelatedReserveId = mainReserveId,
                ReservePaymentId = parentId
            };
            _context.CustomerAccountTransactions.Add(paymentTransaction);

            customer.CurrentBalance += (totalExpectedAmount - totalProvidedAmount);
            _context.Customers.Update(customer);

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }


    private async Task<Result<Direction?>> GetDirectionAsync(int? locationId, string type)
    {
        if (locationId is null)
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


    public async Task<Result<PagedReportResponseDto<ReservePriceReportResponseDto>>>
    GetReservePriceReport(PagedReportRequestDto<ReservePriceReportFilterRequestDto> requestDto)
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


    public async Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>> GetReserveReport(DateTime reserveDate,
    PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto)
    {
        var query = _context.Reserves.Where(rp => rp.Status == ReserveStatusEnum.Confirmed);

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
                rp.CustomerReserves.Count,
                rp.DepartureHour.ToString(@"hh\:mm"),
                rp.VehicleId,
                rp.DriverId.GetValueOrDefault(),
                rp.CustomerReserves
                  .Select(p => new CustomerReserveReportResponseDto(
                      p.CustomerReserveId,
                      p.CustomerId,
                      $"{p.CustomerFullName}",
                      p.DocumentNumber,
                      p.CustomerEmail,
                      $"{p.Phone1} {p.Phone2}",
                      p.ReserveId,
                      p.DropoffLocationId!.Value,
                      p.DropoffAddress,
                      p.PickupLocationId!.Value,
                      p.PickupAddress,
                      p.Customer.CurrentBalance))
                  .ToList(),
                rp.Service.ReservePrices.Select(p => new ReservePriceReport((int)p.ReserveTypeId, p.Price)).ToList()
            ),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<PagedReportResponseDto<CustomerReserveReportResponseDto>>> GetReserveCustomerReport(
    int reserveId,
    PagedReportRequestDto<CustomerReserveReportFilterRequestDto> requestDto)
    {
        var query = _context.CustomerReserves
            .Where(cr => cr.ReserveId == reserveId);

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.CustomerFullName))
        {
            var nameFilter = requestDto.Filters.CustomerFullName.ToLower();
            query = query.Where(cr =>
                (cr.CustomerFullName).ToLower().Contains(nameFilter));
        }

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.DocumentNumber))
        {
            var docFilter = requestDto.Filters.DocumentNumber.Trim();
            query = query.Where(cr => cr.DocumentNumber.Contains(docFilter));
        }

        if (!string.IsNullOrWhiteSpace(requestDto.Filters.Email))
        {
            var emailFilter = requestDto.Filters.Email.ToLower().Trim();
            query = query.Where(cr => cr.CustomerEmail.ToLower().Contains(emailFilter));
        }

        var sortMappings = new Dictionary<string, Expression<Func<CustomerReserve, object>>>
        {
            ["customerfullname"] = cr => cr.CustomerFullName,
            ["documentnumber"] = cr => cr.DocumentNumber,
        };

        var pagedResult = await query.ToPagedReportAsync<CustomerReserveReportResponseDto, CustomerReserve, CustomerReserveReportFilterRequestDto>(
            requestDto,
            selector: cr => new CustomerReserveReportResponseDto(
                cr.CustomerReserveId,
                cr.CustomerId,
                cr.CustomerFullName,
                cr.DocumentNumber,
                cr.CustomerEmail,
                $"{cr.Phone1} {cr.Phone2}",
                cr.ReserveId,
                cr.DropoffLocationId!.Value,
                cr.DropoffAddress,
                cr.PickupLocationId!.Value,
                cr.PickupAddress,
                cr.Customer.CurrentBalance),
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

    public async Task<Result<bool>> CreatePaymentsAsync(
    int reserveId,
    int customerId,
    List<CreatePaymentRequestDto> payments)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var reserve = await _context.Reserves.FindAsync(reserveId);
            if (reserve is null)
                return Result.Failure<bool>(ReserveError.NotFound);

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer is null)
                return Result.Failure<bool>(CustomerError.NotFound);

            if (payments == null || !payments.Any())
                return Result.Failure<bool>(Error.Validation("Payments.Empty", "Debe proporcionar al menos un pago."));

            var invalidAmounts = payments
                .Select((p, i) => new { Index = i + 1, Amount = p.TransactionAmount })
                .Where(p => p.Amount <= 0)
                .ToList();

            if (invalidAmounts.Any())
            {
                var errorMsg = string.Join(", ", invalidAmounts.Select(p => $"Pago #{p.Index} tiene monto inválido: {p.Amount}"));
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
                return Result.Failure<bool>(Error.Validation("Payments.DuplicatedMethod", $"Los métodos de pago no deben repetirse. Duplicados: {duplicatedList}"));
            }

            var service = await _context.Services
                                        .Include(s => s.ReservePrices)
                                        .SingleOrDefaultAsync(s => s.ServiceId == reserve.ServiceId);

            if (service == null)
                return Result.Failure<bool>(ServiceError.ServiceNotFound);

            var reservePrice = service.ReservePrices
                .FirstOrDefault(p => p.ReserveTypeId == ReserveTypeIdEnum.Ida);

            if (reservePrice == null)
                return Result.Failure<bool>(ReserveError.PriceNotAvailable);

            var expectedAmount = reservePrice.Price;
            var providedAmount = payments.Sum(p => p.TransactionAmount);

            if (expectedAmount != providedAmount)
                return Result.Failure<bool>(
                    ReserveError.InvalidPaymentAmount(expectedAmount, providedAmount));

            foreach (var payment in payments)
            {
                var newPayment = new ReservePayment
                {
                    ReserveId = reserveId,
                    CustomerId = customerId,
                    Amount = payment.TransactionAmount,
                    Method = (PaymentMethodEnum)payment.PaymentMethod,
                    Status = StatusPaymentEnum.Paid
                };

                _context.ReservePayments.Add(newPayment);
            }

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }

    public async Task<Result<bool>> UpdateCustomerReserveAsync(int customerReserveId, CustomerReserveUpdateRequestDto request)
    {
        var reserve = await _context.CustomerReserves
            .SingleOrDefaultAsync(cr => cr.CustomerReserveId == customerReserveId);

        if (reserve == null)
            return Result.Failure<bool>(ReserveError.NotFound);

        if (request.PickupLocationId.HasValue)
        {
            var pickup = await _context.Directions.FindAsync(request.PickupLocationId);
            if (pickup == null)
                return Result.Failure<bool>(Error.NotFound("Pickup.NotFound", "Pickup location not found"));

            reserve.PickupLocationId = pickup.DirectionId;
            reserve.PickupAddress = pickup.Name;

            _context.Entry(reserve).Property(r => r.PickupLocationId).IsModified = true;
            _context.Entry(reserve).Property(r => r.PickupAddress).IsModified = true;
        }

        if (request.DropoffLocationId.HasValue)
        {
            var dropoff = await _context.Directions.FindAsync(request.DropoffLocationId);
            if (dropoff == null)
                return Result.Failure<bool>(Error.NotFound("Dropoff.NotFound", "Dropoff location not found"));

            reserve.DropoffLocationId = dropoff.DirectionId;
            reserve.DropoffAddress = dropoff.Name;

            _context.Entry(reserve).Property(r => r.DropoffLocationId).IsModified = true;
            _context.Entry(reserve).Property(r => r.DropoffAddress).IsModified = true;
        }

        if (request.HasTraveled.HasValue)
        {
            reserve.HasTraveled = request.HasTraveled.Value;
            _context.Entry(reserve).Property(r => r.HasTraveled).IsModified = true;
        }

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }


    public async Task<Result<string>> CreatePassengerReservesExternal(CustomerReserveCreateRequestWrapperExternalDto dto)
    {
        var validationResult = ValidateUserReserveCombination(dto.Items);
        if (validationResult.IsFailure)
            return Result.Failure<string>(validationResult.Error);

        int userIdLogged = _userContext.UserId != 0
            ? _userContext.UserId
            : throw new InvalidOperationException("User context is not set. Ensure the user is authenticated.");

        User userLogged = await _context.Users.FindAsync(userIdLogged)
                         ?? throw new InvalidOperationException("User Logged is not Found");

        List<Reserve> reserves = new List<Reserve>();

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            decimal totalExpectedAmount = 0m;
            Customer? customer = null;

            foreach (var passenger in dto.Items)
            {
                var reserve = await _context.Reserves
                   .Include(r => r.CustomerReserves)
                   .SingleOrDefaultAsync(r => r.ReserveId == passenger.ReserveId);

                if (reserve is null)
                    return Result.Failure<string>(ReserveError.NotFound);

                if (reserve.Status != ReserveStatusEnum.Confirmed)
                    return Result.Failure<string>(ReserveError.NotAvailable);

                var service = await _context.Services
                    .Include(s => s.ReservePrices)
                    .Include(s => s.Origin)
                    .Include(s => s.Destination)
                    .SingleOrDefaultAsync(s => s.ServiceId == reserve.ServiceId);

                var vehicle = await _context.Vehicles.FindAsync(reserve.VehicleId);

                var existingPassengerCount = reserve.CustomerReserves.Count;
                var totalAfterInsert = existingPassengerCount + dto.Items.Count;

                if (totalAfterInsert > vehicle.AvailableQuantity)
                    return Result.Failure<string>(
                        ReserveError.VehicleQuantityNotAvailable(existingPassengerCount, dto.Items.Count, vehicle.AvailableQuantity));

                var reservePrice = service.ReservePrices.SingleOrDefault(p => p.ReserveTypeId == (ReserveTypeIdEnum)passenger.ReserveTypeId);
                if (reservePrice is null || passenger.Price != reservePrice.Price)
                    return Result.Failure<string>(ReserveError.PriceNotAvailable);

                var customerResult = await _customerBusiness.GetOrCreateFromPassengerAsync(passenger);
                if (!customerResult.IsSuccess)
                    return Result.Failure<string>(customerResult.Error);

                customer = customerResult.Value;

                if (reserve.CustomerReserves.Any(cr => cr.CustomerId == customer.CustomerId))
                    return Result.Failure<string>(ReserveError.CustomerAlreadyExists(customer.DocumentNumber));

                var pickupResult = await GetDirectionAsync(passenger.PickupLocationId, "Pickup");
                if (pickupResult.IsFailure) return Result.Failure<string>(pickupResult.Error);

                var dropoffResult = await GetDirectionAsync(passenger.DropoffLocationId, "Dropoff");
                if (dropoffResult.IsFailure) return Result.Failure<string>(dropoffResult.Error);

                var newCustomerReserve = new CustomerReserve
                {
                    UserId = userLogged.CustomerId == customer.CustomerId ?
                                                      userLogged.UserId : null,
                    CustomerId = customer.CustomerId,
                    ReserveId = reserve.ReserveId,
                    DropoffLocationId = passenger.DropoffLocationId,
                    PickupLocationId = passenger.PickupLocationId,
                    HasTraveled = passenger.HasTraveled,
                    Price = reservePrice.Price,
                    IsPayment = passenger.IsPayment,
                    CustomerFullName = $"{customer.FirstName} {customer.LastName}",
                    DestinationCityName = service.Destination.Name,
                    OriginCityName = service.Origin.Name,
                    DriverName = reserve.Driver != null ? $"{reserve.Driver.FirstName} {reserve.Driver.LastName}" : null,
                    PickupAddress = pickupResult.Value?.Name,
                    DropoffAddress = dropoffResult.Value?.Name,
                    ServiceName = service.Name,
                    VehicleInternalNumber = vehicle.InternalNumber,
                    CustomerEmail = customer.Email,
                    DocumentNumber = customer.DocumentNumber,
                    Phone1 = customer.Phone1,
                    Phone2 = customer.Phone2
                };

                reserve.CustomerReserves.Add(newCustomerReserve);
                reserve.Raise(new CustomerReserveCreatedEvent(newCustomerReserve.CustomerReserveId));
                _context.Reserves.Update(reserve);

                reserves.Add(reserve);

                totalExpectedAmount += reservePrice.Price;
            }

            if (dto.Payment is null)
            {
                foreach (var reserve in reserves)
                {
                    foreach (var cr in reserve.CustomerReserves)
                    {
                        cr.Status = CustomerReserveStatusEnum.PendingPayment;
                    }
                }

                var resultPayment = await CreatePendingPayment(totalExpectedAmount, reserves);
                if (resultPayment.IsFailure) return Result.Failure<string>(resultPayment.Error);

                string preferenceId = await _paymentGateway.CreatePreferenceAsync(
                    resultPayment.Value.ToString(),
                    totalExpectedAmount,
                    dto.Items
                );

                await _context.SaveChangesWithOutboxAsync();
                return Result.Success(preferenceId);
            }
            else
            {
                var totalProvidedAmount = dto.Payment.TransactionAmount;

                if (totalExpectedAmount != totalProvidedAmount)
                    return Result.Failure<string>(ReserveError.InvalidPaymentAmount(totalExpectedAmount, totalProvidedAmount));

                var resultPayment = await CreatePayment(dto.Payment, reserves);
                if (resultPayment.IsFailure) return Result.Failure<string>(resultPayment.Error);
            }

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(string.Empty);
        });
    }

    private Result ValidateUserReserveCombination(List<CustomerReserveCreateRequestDto> items)
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


    private async Task<Result<bool>> CreatePayment(CreatePaymentExternalRequestDto paymentData, List<Reserve> reserves)
    {
        var paymentRequest = new PaymentCreateRequest
        {
            TransactionAmount = paymentData.TransactionAmount,
            Token = paymentData.Token,
            Description = paymentData.Description,
            Installments = paymentData.Installments,
            PaymentMethodId = paymentData.PaymentMethodId,
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

        var isPendingApproval = result.Status == "pending" || result.Status == "in_process";

        var statusPaymentInternal = GetPaymentStatusFromExternal(result.Status);
        if (statusPaymentInternal is null)
        {
            return Result.Failure<bool>(
                Error.Validation("Payment.StatusMappingError", $"El estado de pago externo '{result.Status}' no pudo ser interpretado correctamente.")
            );
        }

        var allCustomerReserves = reserves.SelectMany(r => r.CustomerReserves).ToList();

        var payingCustomerReserve = allCustomerReserves
            .FirstOrDefault(cr => cr.DocumentNumber == paymentData.IdentificationNumber)
            ?? allCustomerReserves.First();

        var reserveStatus = isPendingApproval
            ? CustomerReserveStatusEnum.PendingPayment
            : statusPaymentInternal == StatusPaymentEnum.Paid
                ? CustomerReserveStatusEnum.Confirmed
                : CustomerReserveStatusEnum.Cancelled;

        foreach (var reserve in reserves)
        {
            foreach (var cr in reserve.CustomerReserves)
            {
                cr.Status = reserveStatus;
            }
        }

        var parentPayment = new ReservePayment
        {
            PaymentExternalId = result.Id,
            Amount = paymentData.TransactionAmount,
            ReserveId = payingCustomerReserve.ReserveId,
            CustomerId = payingCustomerReserve.CustomerId,
            Method = PaymentMethodEnum.Online,
            Status = statusPaymentInternal.Value,
            StatusDetail = result.StatusDetail,
            ResultApiExternalRawJson = JsonConvert.SerializeObject(result),
        };

        _context.ReservePayments.Add(parentPayment);
        await _context.SaveChangesWithOutboxAsync();

        var parentId = parentPayment.ReservePaymentId;

        foreach (var reserve in allCustomerReserves.Where(cr => cr.CustomerId != payingCustomerReserve.CustomerId))
        {
            var childPayment = new ReservePayment
            {
                PaymentExternalId = result.Id,
                Amount = 0,
                ReserveId = reserve.ReserveId,
                CustomerId = reserve.CustomerId,
                Method = PaymentMethodEnum.Online,
                Status = statusPaymentInternal.Value,
                StatusDetail = result.StatusDetail,
                ParentReservePaymentId = parentId
            };

            _context.ReservePayments.Add(childPayment);
        }

        await _context.SaveChangesWithOutboxAsync();
        return true;
    }

    private async Task<Result<int>> CreatePendingPayment(decimal amount, List<Reserve> reserves)
    {
        var allCustomerReserves = reserves.SelectMany(r => r.CustomerReserves).ToList();

        var payingCustomerReserve = allCustomerReserves.First();

        var parentPayment = new ReservePayment
        {
            Amount = amount,
            ReserveId = payingCustomerReserve.ReserveId,
            CustomerId = payingCustomerReserve.CustomerId,
            Method = PaymentMethodEnum.Online,
            Status = StatusPaymentEnum.Pending,
            StatusDetail = "wallet_pending",
            ResultApiExternalRawJson = null
        };

        _context.ReservePayments.Add(parentPayment);
        await _context.SaveChangesWithOutboxAsync();

        var parentId = parentPayment.ReservePaymentId;

        foreach (var reserve in allCustomerReserves.Where(cr => cr.CustomerId != payingCustomerReserve.CustomerId))
        {
            var childPayment = new ReservePayment
            {
                Amount = 0,
                ReserveId = reserve.ReserveId,
                CustomerId = reserve.CustomerId,
                Method = PaymentMethodEnum.Online,
                Status = StatusPaymentEnum.Pending,
                StatusDetail = "wallet_pending",
                ParentReservePaymentId = parentId
            };

            _context.ReservePayments.Add(childPayment);
        }

        await _context.SaveChangesWithOutboxAsync();
        return parentId;
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
        if (_context.ReservePayments.Any(p => p.PaymentExternalId == long.Parse(externalPaymentId)
        && p.Status != StatusPaymentEnum.Pending))
        {
            return Result.Success(true);
        }

        var reservePayments = _context.ReservePayments.ToList();

        Payment mpPayment = await _paymentGateway.GetPaymentAsync(externalPaymentId);

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Buscar el pago padre por externalId
            var parentPayment = await _context.ReservePayments
                .FirstOrDefaultAsync(rp => rp.ReservePaymentId == int.Parse(mpPayment.ExternalReference));

            if (parentPayment == null)
                return Result.Failure<bool>(Error.NotFound("Payment.NotFound", "No se encontró el pago con el ID externo proporcionado"));

            // Determinar el estado interno
            var internalStatus = GetPaymentStatusFromExternal(mpPayment.Status);
            if (internalStatus == null)
                return Result.Failure<bool>(Error.Validation("Payment.InvalidStatus", "Estado de pago no reconocido"));

            // Actualizar todos los pagos relacionados (padre e hijos)
            var relatedPayments = await _context.ReservePayments
                .Where(rp => rp.ParentReservePaymentId == parentPayment.ReservePaymentId ||
                             rp.ReservePaymentId == parentPayment.ReservePaymentId)
                .ToListAsync();

            foreach (var payment in relatedPayments)
            {
                payment.Status = internalStatus.Value;
                payment.StatusDetail = parentPayment.StatusDetail;
                payment.PaymentExternalId = mpPayment?.Id;
                payment.ResultApiExternalRawJson = JsonConvert.SerializeObject(payment);

                _context.ReservePayments.Update(payment);
                await _context.SaveChangesWithOutboxAsync();
            }

            // Obtener todas las CustomerReserves asociadas a estos pagos
            var reserveIds = relatedPayments.Select(rp => rp.ReserveId).Distinct().ToList();
            var reserves = await _context.Reserves.Include(p => p.CustomerReserves).Where(p => reserveIds.Contains(p.ReserveId))
                .ToListAsync();

            // Actualizar el estado de las CustomerReserves según el resultado del pago
            var newReserveStatus = internalStatus.Value == StatusPaymentEnum.Paid
                ? CustomerReserveStatusEnum.Confirmed
                : CustomerReserveStatusEnum.Cancelled;

            foreach (var reserve in reserves)
            {
                foreach (var cr in reserve.CustomerReserves)
                {
                    cr.Status = newReserveStatus;
                    _context.CustomerReserves.Update(cr);
                }

                await _context.SaveChangesWithOutboxAsync();
            }

            await _context.SaveChangesWithOutboxAsync();
            return Result.Success(true);
        });
    }
}
