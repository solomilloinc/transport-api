using FluentAssertions;
using Moq;
using Transport.Business.Data;
using Transport.Business.ReserveReportBusiness;
using Transport.Domain.Customers;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Reserve;
using Xunit;
using Transport.Domain.Vehicles;
using Transport.Domain.Cities;
using Transport.Domain.Passengers;
using Transport.Domain.Trips;

namespace Transport.Tests;

public class ReserveReportBusinessTest : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly ReserveReportBusiness _reserveBusiness;

    public ReserveReportBusinessTest()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _reserveBusiness = new ReserveReportBusiness(_contextMock.Object);
    }

    [Fact]
    public async Task GetReserveReport_ShouldReturnReserve_WhenAvailableReserveExistsOnRequestedDate()
    {
        // Arrange
        var reserveDate = new DateTime(2024, 10, 5);

        var schedule = new ServiceSchedule
        {
            ServiceScheduleId = 1,
            DepartureHour = new TimeSpan(8, 0, 0),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };

        var vehicle = new Vehicle { AvailableQuantity = 3 };
        var service = new Service
        {
            Trip = new Trip
            {
                OriginCity = new City { Name = "Buenos Aires" },
                DestinationCity = new City { Name = "Córdoba" }
            },
            Vehicle = vehicle,
            Schedules = new List<ServiceSchedule> { schedule }
        };

        var reserves = new List<Reserve>
    {
        new Reserve
        {
            ReserveId = 1,
            ReserveDate = reserveDate,
            Status = ReserveStatusEnum.Confirmed,
            ServiceSchedule = schedule,
            Service = service,
            Vehicle = vehicle,
            OriginName = "Buenos Aires",
            DestinationName = "Córdoba",
            TripId = 1,
            Passengers = new List<Passenger>
            {
                new Passenger
                {
                    ReserveId = 1,
                    CustomerId = 101,
                    DropoffLocationId = 1,
                    PickupLocationId = 2,
                    Customer = new Customer
                    {
                        FirstName = "Ana",
                        LastName = "López",
                        DocumentNumber = "98765432",
                        Email = "ana@example.com",
                        Phone1 = "111",
                        Phone2 = "222"
                    },
                    LastName = "López",
                    FirstName = "Ana",
                    DocumentNumber = "98765432",
                    Email = "ana@example.com",
                    Phone = "111",
                }
            }
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10
        };

        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(x => x.Trips)
            .Returns(GetQueryableMockDbSet(new List<Trip>()));

        // Act
        var result = await _reserveBusiness.GetReserveReport(reserveDate, request);

        // Assert
        Assert.True(result.IsSuccess);
        var item = result.Value.Items.Single();
        Assert.Equal("Buenos Aires", item.OriginName);
        Assert.Equal("Córdoba", item.DestinationName);
        Assert.Equal(3, item.AvailableQuantity);
        Assert.Equal(1, item.ReservedQuantity);
    }

    [Fact]
    public async Task GetReserveReport_ShouldReturnMultipleReserves_WhenMoreThanOneAvailableExists()
    {
        // Arrange
        var reserveDate = new DateTime(2024, 11, 1);

        var schedule = new ServiceSchedule
        {
            ServiceScheduleId = 1,
            DepartureHour = new TimeSpan(8, 0, 0),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };

        var vehicle1 = new Vehicle { AvailableQuantity = 2 };
        var vehicle2 = new Vehicle { AvailableQuantity = 5 };

        var reserves = new List<Reserve>
    {
        new Reserve
        {
            ReserveId = 1,
            ReserveDate = reserveDate,
            Status = ReserveStatusEnum.Confirmed,
            ServiceSchedule = schedule,
            Service = new Service
            {
                Trip = new Trip
                {
                    OriginCity = new City { Name = "Rosario" },
                    DestinationCity = new City { Name = "Santa Fe" }
                },
                Vehicle = vehicle1,
                Schedules = new List<ServiceSchedule> { schedule }
            },
            Vehicle = vehicle1,
            OriginName = "Rosario",
            DestinationName = "Santa Fe",
            TripId = 1,
            Passengers = new List<Passenger>()
        },
        new Reserve
        {
            ReserveId = 2,
            ReserveDate = reserveDate,
            Status = ReserveStatusEnum.Confirmed,
            ServiceSchedule = schedule,
            Service = new Service
            {
                Trip = new Trip
                {
                    OriginCity = new City { Name = "Mendoza" },
                    DestinationCity = new City { Name = "San Juan" }
                },
                Vehicle = vehicle2,
                Schedules = new List<ServiceSchedule> { schedule }
            },
            Vehicle = vehicle2,
            OriginName = "Mendoza",
            DestinationName = "San Juan",
            TripId = 2,
            Passengers = new List<Passenger>()
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10
        };

        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(x => x.Trips)
            .Returns(GetQueryableMockDbSet(new List<Trip>()));

        // Act
        var result = await _reserveBusiness.GetReserveReport(reserveDate, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Contains(result.Value.Items, r => r.OriginName == "Rosario");
        Assert.Contains(result.Value.Items, r => r.OriginName == "Mendoza");
    }


    [Fact]
    public async Task GetReserveReport_ShouldReturnEmptyList_WhenNoReserveMatchesDate()
    {
        // Arrange
        var requestDate = new DateTime(2024, 12, 25);

        var reserves = new List<Reserve>
    {
        new Reserve
        {
            ReserveId = 1,
            ReserveDate = new DateTime(2024, 12, 24),
            Status = ReserveStatusEnum.Available,
            Service = new Service
            {
                Trip = new Trip
                {
                    OriginCity = new City { Name = "Salta" },
                    DestinationCity = new City { Name = "Jujuy" }
                },
                Vehicle = new Vehicle { AvailableQuantity = 2 }
            },

            TripId = 1,
            Passengers = new List<Passenger>()
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10
        };

        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(x => x.Trips)
            .Returns(GetQueryableMockDbSet(new List<Trip>()));

        // Act
        var result = await _reserveBusiness.GetReserveReport(requestDate, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task GetReserveReport_ShouldReturnEmptyList_WhenNoAvailableReservesExist()
    {
        // Arrange
        var reserveDate = new DateTime(2024, 9, 1);

        var reserves = new List<Reserve>
    {
        new Reserve
        {
            ReserveId = 1,
            ReserveDate = reserveDate,
            Status = ReserveStatusEnum.Rejected,
            Service = new Service
            {
                Trip = new Trip
                {
                    OriginCity = new City { Name = "La Plata" },
                    DestinationCity = new City { Name = "Mar del Plata" }
                },
                Vehicle = new Vehicle { AvailableQuantity = 2 }
            },

            TripId = 1,
            Passengers = new List<Passenger>()
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10
        };

        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(x => x.Trips)
            .Returns(GetQueryableMockDbSet(new List<Trip>()));

        // Act
        var result = await _reserveBusiness.GetReserveReport(reserveDate, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task GetReserveReport_ShouldReturnOutboundAndReturnWithCorrectOriginDestination_AndPassengersFilter()
    {
        // Arrange
        var departureDate = new DateTime(2025, 1, 15);
        var returnDate = new DateTime(2025, 1, 20);
        var passengersRequested = 2;

        var vehicleIda = new Vehicle { AvailableQuantity = 3, InternalNumber = "V001" };
        var vehicleVuelta = new Vehicle { AvailableQuantity = 3, InternalNumber = "V002" };

        var serviceIda = new Service
        {
            Trip = new Trip
            {
                OriginCityId = 1,
                DestinationCityId = 2,
                OriginCity = new City { CityId = 1, Name = "Lobos" },
                DestinationCity = new City { CityId = 2, Name = "Ciudad Autonoma de Buenos Aires" }
            },
            Vehicle = vehicleIda,
            EstimatedDuration = TimeSpan.FromHours(2)
        };

        var serviceVuelta = new Service
        {
            Trip = new Trip
            {
                OriginCityId = 2,
                DestinationCityId = 1,
                OriginCity = new City { CityId = 2, Name = "Ciudad Autonoma de Buenos Aires" },
                DestinationCity = new City { CityId = 1, Name = "Lobos" }
            },
            Vehicle = vehicleVuelta,
            EstimatedDuration = TimeSpan.FromHours(2)
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, CityId = 2, Price = 10, Status = EntityStatusEnum.Active }
            }},
            new Trip { TripId = 2, OriginCityId = 2, DestinationCityId = 1, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, CityId = 1, Price = 12, Status = EntityStatusEnum.Active }
            }}
        };

        var reserves = new List<Reserve>
        {
            // Ida reserve with 1 passenger confirmed (available quantity 3 - 1 = 2 available, which meets passengersRequested)
            new Reserve
            {
                ReserveId = 100,
                ReserveDate = departureDate,
                Status = ReserveStatusEnum.Confirmed,
                Service = serviceIda,
                Vehicle = vehicleIda,
                Trip = serviceIda.Trip,
                TripId = 1,
                OriginName = "Lobos",
                DestinationName = "Ciudad Autonoma de Buenos Aires",
                DepartureHour = new TimeSpan(9,0,0),
                EstimatedDuration = TimeSpan.FromHours(2),
                Passengers = new List<Passenger>
                {
                    new Passenger
                    {
                        Status = PassengerStatusEnum.Confirmed
                    }
                }
            },
            // Vuelta reserve with 2 passengers confirmed (available quantity 3 - 2 = 1 available, which is less than passengersRequested so should be excluded)
            new Reserve
            {
                ReserveId = 101,
                ReserveDate = returnDate,
                Status = ReserveStatusEnum.Confirmed,
                Service = serviceVuelta,
                Vehicle = vehicleVuelta,
                Trip = serviceVuelta.Trip,
                TripId = 2,
                OriginName = "Ciudad Autonoma de Buenos Aires",
                DestinationName = "Lobos",
                DepartureHour = new TimeSpan(18,0,0),
                EstimatedDuration = TimeSpan.FromHours(2),
                Passengers = new List<Passenger>
                {
                    new Passenger { Status = PassengerStatusEnum.Confirmed },
                    new Passenger { Status = PassengerStatusEnum.Confirmed }
                }
            },
            // Vuelta reserve with 0 passengers confirmed (available quantity 3 - 0 = 3 available, meets passengersRequested)
            new Reserve
            {
                ReserveId = 102,
                ReserveDate = returnDate,
                Status = ReserveStatusEnum.Confirmed,
                Service = serviceVuelta,
                Vehicle = vehicleVuelta,
                Trip = serviceVuelta.Trip,
                TripId = 2,
                OriginName = "Ciudad Autonoma de Buenos Aires",
                DestinationName = "Lobos",
                DepartureHour = new TimeSpan(20,0,0),
                EstimatedDuration = TimeSpan.FromHours(2),
                Passengers = new List<Passenger>() // no passengers
            }
        };

        var filter = new ReserveReportFilterRequestDto(
            TripId: 1,
            TripType: "IdaVuelta",
            Passengers: passengersRequested,
            DepartureDate: departureDate,
            ReturnDate: returnDate
        );

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            Filters = filter,
            PageNumber = 1,
            PageSize = 10
        };

        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(reserves));

        _contextMock.Setup(x => x.Trips)
            .Returns(GetQueryableMockDbSet(trips));

        // Act
        var result = await _reserveBusiness.GetReserveReport(request);

        // Assert
        Assert.True(result.IsSuccess);

        var outbound = result.Value.Outbound;
        var ret = result.Value.Return;

        // Outbound contains the reserve with 1 passenger, so available quantity 3 - 1 = 2 >= passengersRequested 2? Yes
        Assert.Single(outbound.Items);
        var outboundReserve = outbound.Items[0];
        Assert.Equal(100, outboundReserve.ReserveId);
        Assert.Equal("Lobos", outboundReserve.OriginName);
        Assert.Equal("Ciudad Autonoma de Buenos Aires", outboundReserve.DestinationName);
        Assert.Equal("09:00", outboundReserve.DepartureHour);
        Assert.Equal(10, outboundReserve.Price);

        // Return excludes reserveId 101 because passengers > available, includes reserveId 102
        Assert.Single(ret.Items);
        var returnReserve = ret.Items[0];
        Assert.Equal(102, returnReserve.ReserveId);
        Assert.Equal("Ciudad Autonoma de Buenos Aires", returnReserve.OriginName);
        Assert.Equal("Lobos", returnReserve.DestinationName);
        Assert.Equal("20:00", returnReserve.DepartureHour);
        Assert.Equal(12, returnReserve.Price);
    }

    [Fact]
    public async Task GetAvailableReserves_ShouldReturnEmpty_WhenNotEnoughCapacityForPassengers()
    {
        // Arrange
        var departureDate = new DateTime(2024, 10, 10);
        var passengersRequested = 5;

        var schedule = new ServiceSchedule
        {
            ServiceScheduleId = 1,
            DepartureHour = new TimeSpan(9, 0, 0),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };

        var vehicle = new Vehicle { AvailableQuantity = 4 }; // less than passengersRequested

        var service = new Service
        {
            Trip = new Trip
            {
                OriginCityId = 1,
                DestinationCityId = 2,
                OriginCity = new City { Name = "Rosario" },
                DestinationCity = new City { Name = "Santa Fe" }
            },
            Vehicle = vehicle,
            Schedules = new List<ServiceSchedule> { schedule }
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 15m, Status = EntityStatusEnum.Active }
            }}
        };

        var reserve = new Reserve
        {
            ReserveId = 10,
            ReserveDate = departureDate,
            Status = ReserveStatusEnum.Confirmed,
            ServiceSchedule = schedule,
            Service = service,
            Vehicle = vehicle,
            Trip = service.Trip,
            TripId = 1,
            OriginName = "Rosario",
            DestinationName = "Santa Fe",
            EstimatedDuration = TimeSpan.FromHours(1),
            Passengers = new List<Passenger>
        {
            new Passenger { Status = PassengerStatusEnum.Confirmed },
            new Passenger { Status = PassengerStatusEnum.PendingPayment }
        }
        };

        var reserves = new List<Reserve> { reserve };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new ReserveReportFilterRequestDto(
                TripId: 1,
                TripType: "Ida",
                Passengers: passengersRequested,
                DepartureDate: departureDate,
                ReturnDate: null)
        };

        _contextMock.Setup(x => x.Reserves).Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(trips));

        // Act
        var result = await _reserveBusiness.GetReserveReport(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Outbound.Items);  // No reserves because capacity < passengersRequested
        Assert.Empty(result.Value.Return.Items);    // No return trips in this test
    }

    [Fact]
    public async Task GetAvailableReserves_ShouldFilterReturnTrips_CorrectlyConsideringCapacity()
    {
        // Arrange
        var departureDate = new DateTime(2024, 10, 10);
        var returnDate = new DateTime(2024, 10, 12);
        var passengersRequested = 3;

        var schedule = new ServiceSchedule
        {
            ServiceScheduleId = 1,
            DepartureHour = new TimeSpan(9, 0, 0),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };

        var vehicleOutbound = new Vehicle { AvailableQuantity = 5, InternalNumber = "V001" };
        var vehicleReturn = new Vehicle { AvailableQuantity = 2, InternalNumber = "V002" }; // less capacity than requested passengers

        var serviceOutbound = new Service
        {
            Trip = new Trip
            {
                OriginCityId = 1,
                DestinationCityId = 2,
                OriginCity = new City { CityId = 1, Name = "Rosario" },
                DestinationCity = new City { CityId = 2, Name = "Santa Fe" }
            },
            Vehicle = vehicleOutbound,
            Schedules = new List<ServiceSchedule> { schedule },
            EstimatedDuration = TimeSpan.FromHours(1)
        };

        var serviceReturn = new Service
        {
            Trip = new Trip
            {
                OriginCityId = 2,
                DestinationCityId = 1,
                OriginCity = new City { CityId = 2, Name = "Santa Fe" },
                DestinationCity = new City { CityId = 1, Name = "Rosario" }
            },
            Vehicle = vehicleReturn,
            Schedules = new List<ServiceSchedule> { schedule },
            EstimatedDuration = TimeSpan.FromHours(1)
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 20m, Status = EntityStatusEnum.Active }
            }},
            new Trip { TripId = 2, OriginCityId = 2, DestinationCityId = 1, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 20m, Status = EntityStatusEnum.Active }
            }}
        };

        var reserveOutbound = new Reserve
        {
            ReserveId = 21,
            ReserveDate = departureDate,
            Status = ReserveStatusEnum.Confirmed,
            ServiceSchedule = schedule,
            Service = serviceOutbound,
            Vehicle = vehicleOutbound,
            Trip = serviceOutbound.Trip,
            TripId = 1,
            OriginName = "Rosario",
            DestinationName = "Santa Fe",
            EstimatedDuration = TimeSpan.FromHours(1),
            Passengers = new List<Passenger>()
        };

        var reserveReturn = new Reserve
        {
            ReserveId = 22,
            ReserveDate = returnDate,
            Status = ReserveStatusEnum.Confirmed,
            ServiceSchedule = schedule,
            Service = serviceReturn,
            Vehicle = vehicleReturn,
            Trip = serviceReturn.Trip,
            TripId = 2,
            OriginName = "Santa Fe",
            DestinationName = "Rosario",
            EstimatedDuration = TimeSpan.FromHours(1),
            Passengers = new List<Passenger>()
        };

        var reserves = new List<Reserve> { reserveOutbound, reserveReturn };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new ReserveReportFilterRequestDto(
                TripId: 1,
                TripType: "IdaVuelta",
                Passengers: passengersRequested,
                DepartureDate: departureDate,
                ReturnDate: returnDate)
        };

        _contextMock.Setup(x => x.Reserves).Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(trips));

        // Act
        var result = await _reserveBusiness.GetReserveReport(request);

        // Assert
        Assert.True(result.IsSuccess);

        // Outbound has enough capacity so returns one item
        Assert.Single(result.Value.Outbound.Items);
        Assert.Equal("Rosario", result.Value.Outbound.Items[0].OriginName);

        // Return has NOT enough capacity so returns empty list
        Assert.Empty(result.Value.Return.Items);
    }
    #region GetReservePaymentSummary Tests

    [Fact]
    public async Task GetReservePaymentSummary_ShouldFail_WhenReserveNotFound()
    {
        // Arrange
        _contextMock.Setup(c => c.Reserves)
            .Returns(GetQueryableMockDbSet(new List<Reserve>()));

        var request = new PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto>();

        // Act
        var result = await _reserveBusiness.GetReservePaymentSummary(999, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReserveError.NotFound);
    }

    [Fact]
    public async Task GetReservePaymentSummary_ShouldReturnEmptySummary_WhenNoPayments()
    {
        // Arrange
        var reserve = new Reserve { ReserveId = 1 };
        var reserves = new List<Reserve> { reserve };
        var payments = new List<ReservePayment>();

        _contextMock.Setup(c => c.Reserves)
            .Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(c => c.ReservePayments)
            .Returns(GetQueryableMockDbSet(payments));

        var request = new PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto>();

        // Act
        var result = await _reserveBusiness.GetReservePaymentSummary(1, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].ReserveId.Should().Be(1);
        result.Value.Items[0].PaymentsByMethod.Should().BeEmpty();
        result.Value.Items[0].TotalAmount.Should().Be(0);
    }

    [Fact]
    public async Task GetReservePaymentSummary_ShouldReturnCorrectSummary_WithSinglePaymentMethod()
    {
        // Arrange
        var reserve = new Reserve { ReserveId = 1 };
        var reserves = new List<Reserve> { reserve };
        var payments = new List<ReservePayment>
        {
            new ReservePayment
            {
                ReservePaymentId = 1,
                ReserveId = 1,
                Amount = 5000,
                Method = PaymentMethodEnum.Cash,
                ParentReservePaymentId = null
            }
        };

        _contextMock.Setup(c => c.Reserves)
            .Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(c => c.ReservePayments)
            .Returns(GetQueryableMockDbSet(payments));

        var request = new PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto>();

        // Act
        var result = await _reserveBusiness.GetReservePaymentSummary(1, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);

        var summary = result.Value.Items[0];
        summary.ReserveId.Should().Be(1);
        summary.TotalAmount.Should().Be(5000);
        summary.PaymentsByMethod.Should().HaveCount(1);
        summary.PaymentsByMethod[0].PaymentMethodId.Should().Be((int)PaymentMethodEnum.Cash);
        summary.PaymentsByMethod[0].PaymentMethodName.Should().Be("Efectivo");
        summary.PaymentsByMethod[0].Amount.Should().Be(5000);
    }

    [Fact]
    public async Task GetReservePaymentSummary_ShouldReturnCorrectSummary_WithMultiplePaymentMethods()
    {
        // Arrange
        var reserve = new Reserve { ReserveId = 1 };
        var reserves = new List<Reserve> { reserve };

        // Simular pago padre con monto total y hijos de desglose por método
        var payments = new List<ReservePayment>
        {
            // Pago padre (monto total consolidado)
            new ReservePayment
            {
                ReservePaymentId = 1,
                ReserveId = 1,
                Amount = 8000,
                Method = PaymentMethodEnum.Cash,
                ParentReservePaymentId = null
            },
            // Hijo desglose - Efectivo (Breakdown: Amount > 0)
            new ReservePayment
            {
                ReservePaymentId = 2,
                ReserveId = 1,
                Amount = 5000,
                Method = PaymentMethodEnum.Cash,
                ParentReservePaymentId = 1
            },
            // Hijo desglose - Tarjeta (Breakdown: Amount > 0)
            new ReservePayment
            {
                ReservePaymentId = 3,
                ReserveId = 1,
                Amount = 3000,
                Method = PaymentMethodEnum.CreditCard,
                ParentReservePaymentId = 1
            }
        };

        _contextMock.Setup(c => c.Reserves)
            .Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(c => c.ReservePayments)
            .Returns(GetQueryableMockDbSet(payments));

        var request = new PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto>();

        // Act
        var result = await _reserveBusiness.GetReservePaymentSummary(1, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);

        var summary = result.Value.Items[0];
        summary.ReserveId.Should().Be(1);
        summary.TotalAmount.Should().Be(8000, "Total debe ser la suma de pagos padres");
        summary.PaymentsByMethod.Should().HaveCount(2, "Debe haber 2 métodos de pago diferentes");

        var cashPayment = summary.PaymentsByMethod.First(p => p.PaymentMethodId == (int)PaymentMethodEnum.Cash);
        cashPayment.Amount.Should().Be(5000);
        cashPayment.PaymentMethodName.Should().Be("Efectivo");

        var cardPayment = summary.PaymentsByMethod.First(p => p.PaymentMethodId == (int)PaymentMethodEnum.CreditCard);
        cardPayment.Amount.Should().Be(3000);
        cardPayment.PaymentMethodName.Should().Be("Tarjeta de Crédito");
    }

    [Fact]
    public async Task GetReservePaymentSummary_ShouldAggregateMultiplePayments_SameMethod()
    {
        // Arrange - Múltiples pagos del mismo método
        var reserve = new Reserve { ReserveId = 1 };
        var reserves = new List<Reserve> { reserve };

        var payments = new List<ReservePayment>
        {
            new ReservePayment
            {
                ReservePaymentId = 1,
                ReserveId = 1,
                Amount = 2000,
                Method = PaymentMethodEnum.Cash,
                ParentReservePaymentId = null
            },
            new ReservePayment
            {
                ReservePaymentId = 2,
                ReserveId = 1,
                Amount = 3000,
                Method = PaymentMethodEnum.Cash,
                ParentReservePaymentId = null
            }
        };

        _contextMock.Setup(c => c.Reserves)
            .Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(c => c.ReservePayments)
            .Returns(GetQueryableMockDbSet(payments));

        var request = new PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto>();

        // Act
        var result = await _reserveBusiness.GetReservePaymentSummary(1, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var summary = result.Value.Items[0];

        summary.TotalAmount.Should().Be(5000);
        summary.PaymentsByMethod.Should().HaveCount(1);
        summary.PaymentsByMethod[0].Amount.Should().Be(5000, "Debe sumar todos los pagos en efectivo");
    }

    [Fact]
    public async Task GetReservePassengerReport_ShouldIncludeRelatedReservePayments()
    {
        // Arrange
        var reserveId = 1;
        var relatedReserveId = 2;
        var customerId = 10;

        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var service = new Service { Vehicle = vehicle };
        var reserve1 = new Reserve
        {
            ReserveId = reserveId,
            Service = service,
            Vehicle = vehicle,
            Passengers = new List<Passenger>(),

            TripId = 1
        };

        var passengers = new List<Passenger>
        {
            new Passenger
            {
                PassengerId = 1,
                ReserveId = reserveId,
                ReserveRelatedId = relatedReserveId,
                CustomerId = customerId,
                FirstName = "Juan",
                LastName = "Perez",
                DocumentNumber = "123",
                Reserve = reserve1
            }
        };
        reserve1.Passengers = passengers;

        var payments = new List<ReservePayment>
        {
            // Pago en la reserva actual (Ida) - Efectivo 1000
            new ReservePayment
            {
                ReservePaymentId = 1,
                ReserveId = reserveId,
                CustomerId = customerId,
                Amount = 1000,
                Method = PaymentMethodEnum.Cash
            },
            // Pago en la reserva relacionada (Vuelta) - Online 2000
            new ReservePayment
            {
                ReservePaymentId = 2,
                ReserveId = relatedReserveId,
                CustomerId = customerId,
                Amount = 2000,
                Method = PaymentMethodEnum.Online
            }
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, CityId = 2, Price = 100, Status = EntityStatusEnum.Active },
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, CityId = 2, Price = 200, Status = EntityStatusEnum.Active }
            }}
        };

        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers));
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(payments));
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve1 }));
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips));

        var request = new PagedReportRequestDto<PassengerReserveReportFilterRequestDto>
        {
            Filters = new PassengerReserveReportFilterRequestDto(null, null, null)
        };

        // Act
        var result = await _reserveBusiness.GetReservePassengerReport(reserveId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        
        var passengerReport = result.Value.Items[0];
        passengerReport.PaidAmount.Should().Be(3000, "Debe sumar pagos de ambas reservas (1000 + 2000)");
        passengerReport.PaymentMethods.Should().Contain("Efectivo");
        passengerReport.PaymentMethods.Should().Contain("Online");
    }

    [Fact]
    public async Task GetReservePassengerReport_ShouldIncludeServicePrice_WhenNoPaymentsExist()
    {
        // Arrange
        var reserveId = 1;
        var passengerIdUnpaidIda = 1;
        var passengerIdUnpaidIdaVuelta = 2;

        var trips = new List<Trip>
        {
            new Trip { TripId = 1, OriginCityId = 1, DestinationCityId = 2, Status = EntityStatusEnum.Active, Prices = new List<TripPrice> {
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.Ida, CityId = 2, Price = 1500, Status = EntityStatusEnum.Active },
                new TripPrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, CityId = 2, Price = 2500, Status = EntityStatusEnum.Active }
            }}
        };
        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var service = new Service { Vehicle = vehicle };
        var reserve1 = new Reserve
        {
            ReserveId = reserveId,
            Service = service,
            Vehicle = vehicle,
            Passengers = new List<Passenger>(),

            TripId = 1
        };

        var passengers = new List<Passenger>
        {
            // Pasajero 1: Solo Ida, sin pago
            new Passenger
            {
                PassengerId = passengerIdUnpaidIda,
                ReserveId = reserveId,
                FirstName = "Unpaid",
                LastName = "Ida",
                DocumentNumber = "001",
                Reserve = reserve1
            },
            // Pasajero 2: Ida y Vuelta, sin pago
            new Passenger
            {
                PassengerId = passengerIdUnpaidIdaVuelta,
                ReserveId = reserveId,
                ReserveRelatedId = 3, // Indica que es Ida y Vuelta
                FirstName = "Unpaid",
                LastName = "IdaVuelta",
                DocumentNumber = "002",
                Reserve = reserve1
            }
        };
        reserve1.Passengers = passengers;

        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers));
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips));
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(new List<ReservePayment>()));
        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(new List<Reserve> { reserve1 }));

        var request = new PagedReportRequestDto<PassengerReserveReportFilterRequestDto>
        {
            Filters = new PassengerReserveReportFilterRequestDto(null, null, null)
        };

        // Act
        var result = await _reserveBusiness.GetReservePassengerReport(reserveId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        
        var reportIda = result.Value.Items.First(i => i.PassengerId == passengerIdUnpaidIda);
        reportIda.PaidAmount.Should().Be(1500, "Debe mostrar el precio de Ida del servicio");
        reportIda.IsPayment.Should().BeFalse();

        var reportIdaVuelta = result.Value.Items.First(i => i.PassengerId == passengerIdUnpaidIdaVuelta);
        reportIdaVuelta.PaidAmount.Should().Be(2500, "Debe mostrar el precio de IdaVuelta del servicio");
        reportIdaVuelta.IsPayment.Should().BeFalse();
    }

    #endregion

}
