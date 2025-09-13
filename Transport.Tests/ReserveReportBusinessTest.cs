using Moq;
using Transport.Business.Data;
using Transport.Business.ReserveBusiness;
using Transport.Domain.Customers;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;
using Xunit;
using Transport.Domain.Vehicles;
using Transport.Domain.Cities;
using Transport.Business.Authentication;
using Transport.Business.Services.Payment;
using Transport.Domain.Customers.Abstraction;
using Transport.Domain.Passengers;

namespace Transport.Tests;

public class ReserveReportBusinessTest : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserContext> _userContextMock;
    private readonly Mock<IMercadoPagoPaymentGateway> _mercadoPagoPaymentGatewayMock;
    private readonly Mock<ICustomerBusiness> _customerBusinessMock;
    private readonly ReserveBusiness _reserveBusiness;

    public ReserveReportBusinessTest()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userContextMock = new Mock<IUserContext>();
        _mercadoPagoPaymentGatewayMock = new Mock<IMercadoPagoPaymentGateway>();
        _customerBusinessMock = new Mock<ICustomerBusiness>();
        _reserveBusiness = new ReserveBusiness(_contextMock.Object, _unitOfWorkMock.Object, _userContextMock.Object, _mercadoPagoPaymentGatewayMock.Object, _customerBusinessMock.Object, new FakeReserveOption());
    }

    [Fact]
    public async Task GetReserveReport_ShouldReturnReserve_WhenAvailableReserveExistsOnRequestedDate()
    {
        // Arrange
        var reserveDate = new DateTime(2024, 10, 5);

        var schedule = new ServiceSchedule
        {
            ServiceScheduleId = 1,
            StartDay = DayOfWeek.Monday,
            EndDay = DayOfWeek.Friday,
            DepartureHour = new TimeSpan(8, 0, 0),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };

        var service = new Service
        {
            Origin = new City { Name = "Buenos Aires" },
            Destination = new City { Name = "Córdoba" },
            Vehicle = new Vehicle { AvailableQuantity = 3 },
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
            OriginName = "Buenos Aires",
            DestinationName = "Córdoba",
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
            .Returns(GetQueryableMockDbSet(reserves).Object);

        // Act
        var result = await _reserveBusiness.GetReserveReport(reserveDate, request);

        // Assert
        Assert.True(result.IsSuccess);
        var item = result.Value.Items.Single();
        Assert.Equal("Buenos Aires", item.OriginName);
        Assert.Equal("Córdoba", item.DestinationName);
        Assert.Equal(3, item.AvailableQuantity);
        Assert.Single(item.Passengers);
        Assert.Equal("Ana López", item.Passengers[0].FullName);
    }

    [Fact]
    public async Task GetReserveReport_ShouldReturnMultipleReserves_WhenMoreThanOneAvailableExists()
    {
        // Arrange
        var reserveDate = new DateTime(2024, 11, 1);

        var schedule = new ServiceSchedule
        {
            ServiceScheduleId = 1,
            StartDay = DayOfWeek.Monday,
            EndDay = DayOfWeek.Friday,
            DepartureHour = new TimeSpan(8, 0, 0),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };

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
                Origin = new City { Name = "Rosario" },
                Destination = new City { Name = "Santa Fe" },
                Vehicle = new Vehicle { AvailableQuantity = 2 },
                Schedules = new List<ServiceSchedule> { schedule }
            },
            OriginName = "Rosario",
            DestinationName = "Santa Fe",
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
                Origin = new City { Name = "Mendoza" },
                Destination = new City { Name = "San Juan" },
                Vehicle = new Vehicle { AvailableQuantity = 5 },
                Schedules = new List<ServiceSchedule> { schedule }
            },
            OriginName = "Mendoza",
            DestinationName = "San Juan",
            Passengers = new List<Passenger>()
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10
        };

        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(reserves).Object);

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
                Origin = new City { Name = "Salta" },
                Destination = new City { Name = "Jujuy" },
                Vehicle = new Vehicle { AvailableQuantity = 2 }
            },
            Passengers = new List<Passenger>()
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10
        };

        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(reserves).Object);

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
                Origin = new City { Name = "La Plata" },
                Destination = new City { Name = "Mar del Plata" },
                Vehicle = new Vehicle { AvailableQuantity = 2 }
            },
            Passengers = new List<Passenger>()
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10
        };

        _contextMock.Setup(x => x.Reserves)
            .Returns(GetQueryableMockDbSet(reserves).Object);

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

        var serviceIda = new Service
        {
            Origin = new City { Name = "Lobos" },
            Destination = new City { Name = "Ciudad Autonoma de Buenos Aires" },
            Vehicle = new Vehicle { AvailableQuantity = 3 },
            ReservePrices = new List<ReservePrice>
            {
                new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 10 }
            }
        };

        var serviceVuelta = new Service
        {
            Origin = new City { Name = "Ciudad Autonoma de Buenos Aires" },
            Destination = new City { Name = "Lobos" },
            Vehicle = new Vehicle { AvailableQuantity = 3 },
            ReservePrices = new List<ReservePrice>
            {
                new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 12 }
            }
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
                DepartureHour = new TimeSpan(9,0,0),
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
                DepartureHour = new TimeSpan(18,0,0),
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
                DepartureHour = new TimeSpan(20,0,0),
                Passengers = new List<Passenger>() // no passengers
            }
        };

        var filter = new ReserveReportFilterRequestDto(
            OriginId: 0, // Not used in the method filter directly, but could be set
            DestinationId: 0,
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
            .Returns(GetQueryableMockDbSet(reserves).Object);

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
            StartDay = DayOfWeek.Monday,
            EndDay = DayOfWeek.Friday,
            DepartureHour = new TimeSpan(9, 0, 0),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };

        var vehicle = new Vehicle { AvailableQuantity = 4 }; // less than passengersRequested

        var service = new Service
        {
            Origin = new City { Name = "Rosario" },
            Destination = new City { Name = "Santa Fe" },
            Vehicle = vehicle,
            Schedules = new List<ServiceSchedule> { schedule },
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 15m }
        }
        };

        var reserve = new Reserve
        {
            ReserveId = 10,
            ReserveDate = departureDate,
            Status = ReserveStatusEnum.Confirmed,
            ServiceSchedule = schedule,
            Service = service,
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
                OriginId: 1,
                DestinationId: 2,
                TripType: "Ida",
                Passengers: passengersRequested,
                DepartureDate: departureDate,
                ReturnDate: null)
        };

        _contextMock.Setup(x => x.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);

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
            StartDay = DayOfWeek.Monday,
            EndDay = DayOfWeek.Friday,
            DepartureHour = new TimeSpan(9, 0, 0),
            IsHoliday = false,
            Status = EntityStatusEnum.Active
        };

        var vehicleOutbound = new Vehicle { AvailableQuantity = 5 };
        var vehicleReturn = new Vehicle { AvailableQuantity = 2 }; // less capacity than requested passengers

        var serviceOutbound = new Service
        {
            Origin = new City { CityId = 1, Name = "Rosario" },
            Destination = new City { CityId = 2, Name = "Santa Fe" },
            Vehicle = vehicleOutbound,
            Schedules = new List<ServiceSchedule> { schedule },
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 20m }
        }
        };

        var serviceReturn = new Service
        {
            Origin = new City { CityId = 2, Name = "Santa Fe" },
            Destination = new City { CityId = 1, Name = "Rosario" },
            Vehicle = vehicleReturn,
            Schedules = new List<ServiceSchedule> { schedule },
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Price = 20m }
        }
        };

        var reserveOutbound = new Reserve
        {
            ReserveId = 21,
            ReserveDate = departureDate,
            Status = ReserveStatusEnum.Confirmed,
            ServiceSchedule = schedule,
            Service = serviceOutbound,
            Passengers = new List<Passenger>()
        };

        var reserveReturn = new Reserve
        {
            ReserveId = 22,
            ReserveDate = returnDate,
            Status = ReserveStatusEnum.Confirmed,
            ServiceSchedule = schedule,
            Service = serviceReturn,
            Passengers = new List<Passenger>()
        };

        var reserves = new List<Reserve> { reserveOutbound, reserveReturn };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new ReserveReportFilterRequestDto(
                OriginId: 1,
                DestinationId: 2,
                TripType: "IdaVuelta",
                Passengers: passengersRequested,
                DepartureDate: departureDate,
                ReturnDate: returnDate)
        };

        _contextMock.Setup(x => x.Reserves).Returns(GetQueryableMockDbSet(reserves).Object);

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

}
