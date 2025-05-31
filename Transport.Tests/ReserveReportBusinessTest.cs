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
using static Google.Protobuf.Reflection.SourceCodeInfo.Types;

namespace Transport.Tests;

public class ReserveReportBusinessTest : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ReserveBusiness _reserveBusiness;

    public ReserveReportBusinessTest()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _reserveBusiness = new ReserveBusiness(_contextMock.Object, _unitOfWorkMock.Object);
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
            CustomerReserves = new List<CustomerReserve>
            {
                new CustomerReserve
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
                    }
                }
            }
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            Filters = new ReserveReportFilterRequestDto(),
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
            CustomerReserves = new List<CustomerReserve>()
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
            CustomerReserves = new List<CustomerReserve>()
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            Filters = new ReserveReportFilterRequestDto(),
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
            CustomerReserves = new List<CustomerReserve>()
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            Filters = new ReserveReportFilterRequestDto(),
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
            CustomerReserves = new List<CustomerReserve>()
        }
    };

        var request = new PagedReportRequestDto<ReserveReportFilterRequestDto>
        {
            Filters = new ReserveReportFilterRequestDto(),
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

}
