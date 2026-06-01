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
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly ReserveReportBusiness _reserveBusiness;

    // "Ahora" controlable para los tests de HasDeparted / OverdueBalance. La lambda re-evalúa el
    // field en cada llamada, así un test puede ajustarlo antes del Act.
    private DateTime _utcNow = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    public ReserveReportBusinessTest()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(() => _utcNow);
        // LocalNow = ahora en hora Argentina (UTC−3): UtcNow − 3h.
        _dateTimeProviderMock.Setup(x => x.LocalNow).Returns(() => _utcNow.AddHours(-3));
        _reserveBusiness = new ReserveReportBusiness(_contextMock.Object, _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task GetReserveReport_ShouldReturnReserve_WhenAvailableReserveExistsOnRequestedDate()
    {
        // Arrange
        var reserveDate = new DateTime(2024, 10, 5);

        var vehicle = new Vehicle { AvailableQuantity = 3 };
        var service = new Service
        {
            Trip = new Trip
            {
                OriginCity = new City { Name = "Buenos Aires" },
                DestinationCity = new City { Name = "Córdoba" }
            },
            Vehicle = vehicle
        };

        var reserves = new List<Reserve>
    {
        new Reserve
        {
            ReserveId = 1,
            ReserveDate = reserveDate,
            Status = ReserveStatusEnum.Confirmed,
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

        var request = new PagedReportRequestDto<ReserveDayReportFilterDto>
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
        var item = result.Value.Reserves.Items.Single();
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

        var vehicle1 = new Vehicle { AvailableQuantity = 2 };
        var vehicle2 = new Vehicle { AvailableQuantity = 5 };

        var reserves = new List<Reserve>
    {
        new Reserve
        {
            ReserveId = 1,
            ReserveDate = reserveDate,
            Status = ReserveStatusEnum.Confirmed,
            Service = new Service
            {
                Trip = new Trip
                {
                    OriginCity = new City { Name = "Rosario" },
                    DestinationCity = new City { Name = "Santa Fe" }
                },
                Vehicle = vehicle1,
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
            Service = new Service
            {
                Trip = new Trip
                {
                    OriginCity = new City { Name = "Mendoza" },
                    DestinationCity = new City { Name = "San Juan" }
                },
                Vehicle = vehicle2,
            },
            Vehicle = vehicle2,
            OriginName = "Mendoza",
            DestinationName = "San Juan",
            TripId = 2,
            Passengers = new List<Passenger>()
        }
    };

        var request = new PagedReportRequestDto<ReserveDayReportFilterDto>
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
        Assert.Equal(2, result.Value.Reserves.Items.Count);
        Assert.Contains(result.Value.Reserves.Items, r => r.OriginName == "Rosario");
        Assert.Contains(result.Value.Reserves.Items, r => r.OriginName == "Mendoza");
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

        var request = new PagedReportRequestDto<ReserveDayReportFilterDto>
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
        Assert.Empty(result.Value.Reserves.Items);
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

        var request = new PagedReportRequestDto<ReserveDayReportFilterDto>
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
        Assert.Empty(result.Value.Reserves.Items);
    }

    [Fact]
    public async Task GetReserveReport_ShouldFilterReservesByTrip_ButKeepAllTripsInFacet()
    {
        // Arrange — dos reservas del mismo día en sentidos opuestos (Trips 10 y 20)
        var reserveDate = new DateTime(2026, 5, 31);
        var vehicle = new Vehicle { AvailableQuantity = 4 };

        var reserves = new List<Reserve>
        {
            new Reserve
            {
                ReserveId = 1, ReserveDate = reserveDate, Status = ReserveStatusEnum.Confirmed,
                Vehicle = vehicle, TripId = 10,
                OriginName = "Lobos", DestinationName = "Capital Federal",
                Passengers = new List<Passenger>()
            },
            new Reserve
            {
                ReserveId = 2, ReserveDate = reserveDate, Status = ReserveStatusEnum.Confirmed,
                Vehicle = vehicle, TripId = 20,
                OriginName = "Capital Federal", DestinationName = "Lobos",
                Passengers = new List<Passenger>()
            }
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 10, Description = "Lobos → Capital Federal" },
            new Trip { TripId = 20, Description = "Capital Federal → Lobos" }
        };

        var request = new PagedReportRequestDto<ReserveDayReportFilterDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new ReserveDayReportFilterDto(TripId: 20) // pierna de vuelta: Trip inverso
        };

        _contextMock.Setup(x => x.Reserves).Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(trips));

        // Act
        var result = await _reserveBusiness.GetReserveReport(reserveDate, request);

        // Assert
        Assert.True(result.IsSuccess);

        // La lista trae SOLO la reserva del Trip filtrado (la inversa), no ambos sentidos
        var reserve = result.Value.Reserves.Items.Single();
        Assert.Equal(2, reserve.ReserveId);
        Assert.Equal("Capital Federal", reserve.OriginName);
        Assert.Equal("Lobos", reserve.DestinationName);

        // El facet expone los dos Trips del día, independiente del filtro aplicado
        Assert.Equal(2, result.Value.AvailableTrips.Count);
        Assert.Contains(result.Value.AvailableTrips, t => t.TripId == 10 && t.Description == "Lobos → Capital Federal");
        Assert.Contains(result.Value.AvailableTrips, t => t.TripId == 20 && t.Description == "Capital Federal → Lobos");
    }

    [Fact]
    public async Task GetReserveReport_ShouldReturnAllReservesOfDay_WhenNoTripFilter()
    {
        // Arrange
        var reserveDate = new DateTime(2026, 5, 31);
        var vehicle = new Vehicle { AvailableQuantity = 4 };

        var reserves = new List<Reserve>
        {
            new Reserve { ReserveId = 1, ReserveDate = reserveDate, Status = ReserveStatusEnum.Confirmed, Vehicle = vehicle, TripId = 10, OriginName = "Lobos", DestinationName = "Capital Federal", Passengers = new List<Passenger>() },
            new Reserve { ReserveId = 2, ReserveDate = reserveDate, Status = ReserveStatusEnum.Confirmed, Vehicle = vehicle, TripId = 20, OriginName = "Capital Federal", DestinationName = "Lobos", Passengers = new List<Passenger>() }
        };

        var trips = new List<Trip>
        {
            new Trip { TripId = 10, Description = "Lobos → Capital Federal" },
            new Trip { TripId = 20, Description = "Capital Federal → Lobos" }
        };

        var request = new PagedReportRequestDto<ReserveDayReportFilterDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new ReserveDayReportFilterDto(TripId: null) // página de Reservas sin selección ⇒ todas
        };

        _contextMock.Setup(x => x.Reserves).Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(x => x.Trips).Returns(GetQueryableMockDbSet(trips));

        // Act
        var result = await _reserveBusiness.GetReserveReport(reserveDate, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Reserves.Items.Count);
        Assert.Equal(2, result.Value.AvailableTrips.Count);
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
            Vehicle = vehicle
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
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetQueryableMockDbSet(new List<CustomerAccountTransaction>()));

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

    [Fact]
    public async Task GetReservePassengerReport_OverdueBalance_CountsOnlyDepartedReservesNetOfPayments()
    {
        // Arrange: hoy = 30-may 12:00. La reserva pasada (27-may) ya viajó; la futura (2-jun) no.
        _utcNow = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
        var reserveId = 1;
        var customerId = 10;

        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var reserve1 = new Reserve { ReserveId = reserveId, Vehicle = vehicle, TripId = 1, Passengers = new List<Passenger>() };

        var customer = new Customer
        {
            CustomerId = customerId,
            FirstName = "Ana",
            LastName = "Lopez",
            DocumentNumber = "1",
            Email = "ana@example.com",
            Phone1 = "1",
            CurrentBalance = 1200 // total histórico (1000 + 500 - 300), incluye la futura
        };

        var passengers = new List<Passenger>
        {
            new Passenger
            {
                PassengerId = 1,
                ReserveId = reserveId,
                CustomerId = customerId,
                Customer = customer,
                FirstName = "Ana",
                LastName = "Lopez",
                DocumentNumber = "1",
                Reserve = reserve1
            }
        };
        reserve1.Passengers = passengers;

        var pastReserve = new Reserve { ReserveId = 50, ReserveDate = new DateTime(2026, 5, 27), DepartureHour = new TimeSpan(8, 0, 0) };
        var futureReserve = new Reserve { ReserveId = 51, ReserveDate = new DateTime(2026, 6, 2), DepartureHour = new TimeSpan(8, 0, 0) };

        // Amount viene firmado: Charge +, Payment − (igual que en la base).
        var transactions = new List<CustomerAccountTransaction>
        {
            new CustomerAccountTransaction { CustomerId = customerId, Type = TransactionType.Charge, Amount = 1000, RelatedReserveId = 50, RelatedReserve = pastReserve },
            new CustomerAccountTransaction { CustomerId = customerId, Type = TransactionType.Payment, Amount = -300, RelatedReserveId = 50, RelatedReserve = pastReserve },
            new CustomerAccountTransaction { CustomerId = customerId, Type = TransactionType.Charge, Amount = 500, RelatedReserveId = 51, RelatedReserve = futureReserve }
        };

        var trips = new List<Trip> { new Trip { TripId = 1, Status = EntityStatusEnum.Active, Prices = new List<TripPrice>() } };

        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers));
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(new List<ReservePayment>()));
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips));
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetQueryableMockDbSet(transactions));

        var request = new PagedReportRequestDto<PassengerReserveReportFilterRequestDto>
        {
            Filters = new PassengerReserveReportFilterRequestDto(null, null, null)
        };

        // Act
        var result = await _reserveBusiness.GetReservePassengerReport(reserveId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Single();
        item.CurrentBalance.Should().Be(1200, "el saldo total no se toca");
        item.OverdueBalance.Should().Be(700, "solo la reserva ya partida: 1000 cargo - 300 pago; la futura (500) se excluye");
    }

    [Fact]
    public async Task GetReservePassengerReport_OverdueBalance_IsZero_WhenDepartedReserveFullyPaid_AndDebtIsFutureOnly()
    {
        // Caso real reportado: reserva #1 ya partida y PAGADA (Cargo +10000, Pago -10000 = 0),
        // más reserva #2003 a futuro impaga (Cargo +10000). La deuda vencida debe ser 0
        // (no se cobra el viaje futuro); CurrentBalance total = 10000. La versión buggy daba 20000.
        _utcNow = new DateTime(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc); // LocalNow = 31-may 09:00
        var reserveId = 2003;
        var customerId = 1;

        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var viewedReserve = new Reserve { ReserveId = reserveId, Vehicle = vehicle, TripId = 1, Passengers = new List<Passenger>() };

        var customer = new Customer
        {
            CustomerId = customerId, FirstName = "Agustin", LastName = "Yuse",
            DocumentNumber = "37976806", Email = "a@a.com", Phone1 = "1", CurrentBalance = 10000m
        };

        var passengers = new List<Passenger>
        {
            new Passenger { PassengerId = 1002, ReserveId = reserveId, CustomerId = customerId, Customer = customer,
                FirstName = "Agustin", LastName = "Yuse", DocumentNumber = "37976806", Reserve = viewedReserve,
                Status = PassengerStatusEnum.PendingPayment }
        };
        viewedReserve.Passengers = passengers;

        var departedReserve = new Reserve { ReserveId = 1, ReserveDate = new DateTime(2026, 5, 30), DepartureHour = TimeSpan.Zero };
        var futureReserveTx = new Reserve { ReserveId = 2003, ReserveDate = new DateTime(2026, 6, 5), DepartureHour = TimeSpan.Zero };

        var transactions = new List<CustomerAccountTransaction>
        {
            new CustomerAccountTransaction { CustomerId = customerId, Type = TransactionType.Charge, Amount = 10000, RelatedReserveId = 1, RelatedReserve = departedReserve },
            new CustomerAccountTransaction { CustomerId = customerId, Type = TransactionType.Payment, Amount = -10000, RelatedReserveId = 1, RelatedReserve = departedReserve },
            new CustomerAccountTransaction { CustomerId = customerId, Type = TransactionType.Charge, Amount = 10000, RelatedReserveId = 2003, RelatedReserve = futureReserveTx }
        };

        var trips = new List<Trip> { new Trip { TripId = 1, Status = EntityStatusEnum.Active, Prices = new List<TripPrice>() } };

        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers));
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(new List<ReservePayment>()));
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips));
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetQueryableMockDbSet(transactions));

        var request = new PagedReportRequestDto<PassengerReserveReportFilterRequestDto>
        {
            Filters = new PassengerReserveReportFilterRequestDto(null, null, null)
        };

        var result = await _reserveBusiness.GetReservePassengerReport(reserveId, request);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Single();
        item.CurrentBalance.Should().Be(10000m, "saldo total incluye el viaje futuro");
        item.OverdueBalance.Should().Be(0m, "la #1 partida está paga (10000 - 10000); la #2003 futura se excluye");
    }

    [Fact]
    public async Task GetReservePassengerReport_OverdueBalance_IsNull_ForPassengerWithoutCustomer()
    {
        // Arrange
        _utcNow = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
        var reserveId = 1;

        var vehicle = new Vehicle { AvailableQuantity = 10 };
        var reserve1 = new Reserve { ReserveId = reserveId, Vehicle = vehicle, TripId = 1, Passengers = new List<Passenger>() };

        var passengers = new List<Passenger>
        {
            new Passenger { PassengerId = 1, ReserveId = reserveId, FirstName = "Sin", LastName = "Cliente", DocumentNumber = "9", Reserve = reserve1 }
        };
        reserve1.Passengers = passengers;

        var trips = new List<Trip> { new Trip { TripId = 1, Status = EntityStatusEnum.Active, Prices = new List<TripPrice>() } };

        _contextMock.Setup(c => c.Passengers).Returns(GetQueryableMockDbSet(passengers));
        _contextMock.Setup(c => c.ReservePayments).Returns(GetQueryableMockDbSet(new List<ReservePayment>()));
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips));
        _contextMock.Setup(c => c.CustomerAccountTransactions).Returns(GetQueryableMockDbSet(new List<CustomerAccountTransaction>()));

        var request = new PagedReportRequestDto<PassengerReserveReportFilterRequestDto>
        {
            Filters = new PassengerReserveReportFilterRequestDto(null, null, null)
        };

        // Act
        var result = await _reserveBusiness.GetReservePassengerReport(reserveId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Single().OverdueBalance.Should().BeNull("un pasajero sin Customer no tiene cuenta corriente");
    }

    [Fact]
    public async Task GetReserveReport_FlagsHasDeparted_InterpretingDepartureAsLocalTime()
    {
        // Arrange: now = 12:00 UTC ⇒ LocalNow = 09:00 local Argentina (UTC−3).
        // La de 08:00 local ya salió; la de 10:00 local TODAVÍA NO (recién a las 09:00 local).
        // El caso de 10:00 es discriminante: comparar el local contra UtcNow (10 < 12) lo
        // marcaría partido por error; comparar contra LocalNow (10 > 09) no.
        _utcNow = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
        var date = new DateTime(2026, 5, 30);

        var vehicle = new Vehicle { AvailableQuantity = 5 };
        var departed = new Reserve
        {
            ReserveId = 1,
            ReserveDate = date,
            DepartureHour = new TimeSpan(8, 0, 0),
            Status = ReserveStatusEnum.Confirmed,
            Vehicle = vehicle,
            TripId = 1,
            OriginName = "A",
            DestinationName = "B",
            Passengers = new List<Passenger>()
        };
        var notYetDeparted = new Reserve
        {
            ReserveId = 2,
            ReserveDate = date,
            DepartureHour = new TimeSpan(10, 0, 0),
            Status = ReserveStatusEnum.Confirmed,
            Vehicle = vehicle,
            TripId = 1,
            OriginName = "A",
            DestinationName = "B",
            Passengers = new List<Passenger>()
        };
        var reserves = new List<Reserve> { departed, notYetDeparted };

        var trips = new List<Trip> { new Trip { TripId = 1, Description = "A-B" } };

        _contextMock.Setup(c => c.Reserves).Returns(GetQueryableMockDbSet(reserves));
        _contextMock.Setup(c => c.Trips).Returns(GetQueryableMockDbSet(trips));

        var request = new PagedReportRequestDto<ReserveDayReportFilterDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new ReserveDayReportFilterDto(null)
        };

        // Act
        var result = await _reserveBusiness.GetReserveReport(date, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var items = result.Value.Reserves.Items;
        items.Should().HaveCount(2);
        items.Single(i => i.ReserveId == 1).HasDeparted.Should().BeTrue("08:00 local ya pasó las 09:00 local actuales");
        items.Single(i => i.ReserveId == 2).HasDeparted.Should().BeFalse("10:00 local todavía no llega (son las 09:00 local), aunque 10 < 12 en UTC");
    }

    #endregion

}
