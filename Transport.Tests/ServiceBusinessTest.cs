﻿using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Transport.Business.Data;
using Transport.Business.ServiceBusiness;
using Transport.Domain;
using Transport.Domain.Cities;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;
using Transport.SharedKernel.Configuration;
using Transport.SharedKernel.Contracts.Service;
using Xunit;

namespace Transport.Tests.ServiceBusinessTests;

public class ServiceBusinessTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly ServiceBusiness _serviceBusiness;

    public ServiceBusinessTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(15);

        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(new DateTime(2025, 05, 12));

        _serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenVehicleNotFound()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync((Vehicle)null);

        var schedules = new List<ServiceScheduleCreateDto>
    {
        new(0,DayOfWeek.Monday, DayOfWeek.Friday, false, TimeSpan.FromHours(8))
    };

        var requestDto = new ServiceCreateRequestDto(
            Name: "Test",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(1),
            VehicleId: 999,
            schedules
        );

        var result = await _serviceBusiness.Create(requestDto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotFound);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenVehicleNotAvailable()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Inactive });

        var schedules = new List<ServiceScheduleCreateDto>
    {
        new(0,DayOfWeek.Monday, DayOfWeek.Friday, false, TimeSpan.FromHours(8))
    };

        var request = new ServiceCreateRequestDto(
            Name: "Test",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(1),
            VehicleId: 1,
            schedules
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(VehicleError.VehicleNotAvailable);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenOriginCityNotFound()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active });

        _contextMock.Setup(x => x.Cities.FindAsync(It.IsAny<int>()))
            .ReturnsAsync((City)null);

        var schedules = new List<ServiceScheduleCreateDto>
    {
        new(0,DayOfWeek.Monday, DayOfWeek.Friday, false, TimeSpan.FromHours(8))
    };

        var request = new ServiceCreateRequestDto(
            Name: "Test",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(1),
            VehicleId: 1,
            schedules
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CityError.CityNotFound);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenDestinationCityNotFound()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active });

        _contextMock.SetupSequence(x => x.Cities.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new City { CityId = 1 })
            .ReturnsAsync((City)null);

        var schedules = new List<ServiceScheduleCreateDto>
    {
        new(0,DayOfWeek.Monday, DayOfWeek.Friday, false, TimeSpan.FromHours(8))
    };

        var request = new ServiceCreateRequestDto(
            Name: "Test",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(1),
            VehicleId: 1,
            schedules
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CityError.CityNotFound);
    }

    [Fact]
    public async Task Create_ShouldSucceed_WhenDataIsValid()
    {
        _contextMock.Setup(x => x.Vehicles.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active });

        _contextMock.SetupSequence(x => x.Cities.FindAsync(It.IsAny<int>()))
            .ReturnsAsync(new City { CityId = 1 }) // Origin
            .ReturnsAsync(new City { CityId = 2 }); // Destination

        _contextMock.Setup(x => x.Services.Add(It.IsAny<Service>()))
            .Callback<Service>(s => s.ServiceId = 99);

        _contextMock.Setup(x => x.ServiceSchedules)
       .Returns(GetQueryableMockDbSet(new List<ServiceSchedule>()).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var schedules = new List<ServiceScheduleCreateDto>
    {
        new(0,  DayOfWeek.Monday, DayOfWeek.Friday, false, TimeSpan.FromHours(8))
    };

        var request = new ServiceCreateRequestDto(
            Name: "TestService",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(3),
            VehicleId: 1,
            schedules
        );

        var result = await _serviceBusiness.Create(request);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetServiceReport_ShouldReturnFilteredPagedResult()
    {
        // Arrange
        var services = new List<Service>
    {
        new Service
        {
            ServiceId = 1,
            Name = "Servicio A",
            Origin = new City { CityId = 1, Name = "Ciudad A" },
            Destination = new City { CityId = 2, Name = "Ciudad B" },
            EstimatedDuration = TimeSpan.FromHours(2),
            Vehicle = new Vehicle
            {
                VehicleId = 1,
                InternalNumber = "V001",
                AvailableQuantity = 5,
                VehicleType = new VehicleType
                {
                    Quantity = 20,
                    Name = "Minibus",
                    ImageBase64 = "base64string"
                }
            },
            Status = EntityStatusEnum.Active
        },
        new Service
        {
            ServiceId = 2,
            Name = "Servicio B",
            Origin = new City { CityId = 3, Name = "Ciudad C" },
            Destination = new City { CityId = 4, Name = "Ciudad D" },
            EstimatedDuration = TimeSpan.FromHours(1),
            Vehicle = new Vehicle
            {
                VehicleId = 2,
                InternalNumber = "V002",
                AvailableQuantity = 3,
                VehicleType = new VehicleType
                {
                    Quantity = 15,
                    Name = "Bus",
                    ImageBase64 = "base64img"
                }
            },
            Status = EntityStatusEnum.Inactive
        }
    };

        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(services).Object);

        var requestDto = new PagedReportRequestDto<ServiceReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new ServiceReportFilterRequestDto("Servicio A", null, null, null, null, null),
            SortBy = "name",
            SortDescending = false
        };

        // Act
        var result = await _serviceBusiness.GetServiceReport(requestDto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Name.Should().Be("Servicio A");
        result.Value.Items.First().OriginName.Should().Be("Ciudad A");
        result.Value.Items.First().DestinationName.Should().Be("Ciudad B");
    }

    [Fact]
    public async Task Update_ShouldFail_WhenServiceNotFound()
    {
        var service = new Service
        {
            ServiceId = 2,
            ReservePrices = new List<ReservePrice>()
        };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var schedules = new List<ServiceScheduleCreateDto>
    {
        new(0,DayOfWeek.Monday, DayOfWeek.Friday, false, TimeSpan.FromHours(8))
    };

        var result = await _serviceBusiness.Update(1, new ServiceCreateRequestDto(
            Name: "Updated",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            VehicleId: 1,
            schedules
        ));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ServiceError.ServiceNotFound);
    }

    [Fact]
    public async Task Update_ShouldSucceed_WhenServiceExists()
    {
        var service = new Service
        {
            ServiceId = 1,
            ReservePrices = new List<ReservePrice>()
        };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);

        _contextMock.Setup(x => x.ServiceSchedules)
       .Returns(GetQueryableMockDbSet(new List<ServiceSchedule>()).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var schedules = new List<ServiceScheduleCreateDto>
    {
        new(0,DayOfWeek.Monday, DayOfWeek.Friday, false, TimeSpan.FromHours(8))
    };

        var dto = new ServiceCreateRequestDto(
            Name: "Updated",
            OriginId: 1,
            DestinationId: 2,
            EstimatedDuration: TimeSpan.FromHours(2),
            VehicleId: 1,
            schedules
        );

        var result = await _serviceBusiness.Update(1, dto);

        result.IsSuccess.Should().BeTrue();
        service.Name.Should().Be(dto.Name);
    }


    [Fact]
    public async Task GenerateFutureReserves_ShouldCreateExpectedReserves_WithServiceSchedules()
    {
        // Arrange
        var today = _dateTimeProviderMock.Object.UtcNow;
        var dayOfWeek = (int)today.DayOfWeek;

        var vehicle = new Vehicle
        {
            VehicleId = 1,
            InternalNumber = "ABC123",
            Status = EntityStatusEnum.Active,
            AvailableQuantity = 10,
            VehicleType = new VehicleType { Name = "Bus", Quantity = 50 }
        };

        var service = new Service
        {
            ServiceId = 1,
            Name = "Mocked Service",
            EstimatedDuration = TimeSpan.FromHours(2),
            Vehicle = vehicle,
            VehicleId = vehicle.VehicleId,
            OriginId = 1,
            Origin = new City() { CityId = 1, Name = "Origin City" },
            DestinationId = 2,
            Destination = new City() { CityId = 2, Name = "Destination City" },
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice
            {
                ReservePriceId = 1,
                Price = 100,
                ReserveTypeId = ReserveTypeIdEnum.Ida
            }
        },
            Schedules = new List<ServiceSchedule>
        {
            new ServiceSchedule
            {
                ServiceScheduleId = 1,
                StartDay = today.DayOfWeek,
                EndDay = today.DayOfWeek,
                DepartureHour = TimeSpan.FromHours(8),
                IsHoliday = false
            }
        }
        };

        var services = new List<Service> { service };
        var vehicles = new List<Vehicle> { vehicle };
        var reserves = new List<Reserve>();
        var holidays = new List<Holiday>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services).Object);
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity(vehicles).Object);
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves).Object);
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Act
        await _serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.NotEmpty(reserves);
        Assert.All(reserves, r =>
        {
            Assert.Equal(vehicle.VehicleId, r.VehicleId);
            Assert.Equal(ReserveStatusEnum.Confirmed, r.Status);
            Assert.InRange(r.ReserveDate.Date, today.Date, today.AddDays(15).Date);
        });
    }

    [Fact]
    public async Task GenerateFutureReserves_ShouldNotCreateReserves_WhenServiceScheduleDaysDoNotMatchUpcomingDays()
    {
        // Arrange
        var today = new DateTime(2025, 05, 12); // Lunes
        var dateProviderMock = new Mock<IDateTimeProvider>();
        dateProviderMock.Setup(x => x.UtcNow).Returns(today);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(3); // Martes a Jueves

        var vehicle = new Vehicle
        {
            VehicleId = 1,
            Status = EntityStatusEnum.Active,
            AvailableQuantity = 5
        };

        var service = new Service
        {
            ServiceId = 1,
            EstimatedDuration = TimeSpan.FromHours(2),
            VehicleId = vehicle.VehicleId,
            Vehicle = vehicle,
            Schedules = new List<ServiceSchedule>
        {
            new ServiceSchedule
            {
                ServiceScheduleId = 1,
                StartDay = DayOfWeek.Sunday,
                EndDay = DayOfWeek.Sunday, // Solo domingo
                DepartureHour = TimeSpan.FromHours(8),
                IsHoliday = false
            }
        },
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { Price = 100, ReserveTypeId = ReserveTypeIdEnum.Ida }
        }
        };

        var services = new List<Service> { service };
        var reserves = new List<Reserve>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services).Object);
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity([vehicle]).Object);
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves).Object);
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(new List<Holiday>()).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, dateProviderMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.Empty(reserves);
    }


    [Fact]
    public async Task GenerateFutureReserves_ShouldSkipHolidayDates_WhenServiceScheduleIsNotHoliday()
    {
        // Arrange
        var today = new DateTime(2025, 05, 12); // Lunes
        var feriado = today.AddDays(1); // Martes

        var dateProviderMock = new Mock<IDateTimeProvider>();
        dateProviderMock.Setup(x => x.UtcNow).Returns(today);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(2); // Lunes y martes

        var vehicle = new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active };

        var service = new Service
        {
            ServiceId = 1,
            EstimatedDuration = TimeSpan.FromHours(2),
            VehicleId = vehicle.VehicleId,
            Vehicle = vehicle,
            OriginId = 1,
            Origin = new City() { CityId = 1, Name = "Origin City" },
            DestinationId = 2,
            Destination = new City() { CityId = 2, Name = "Destination City" },
            Schedules = new List<ServiceSchedule>
        {
            new ServiceSchedule
            {
                ServiceScheduleId = 1,
                StartDay = DayOfWeek.Monday,
                EndDay = DayOfWeek.Tuesday,
                DepartureHour = TimeSpan.FromHours(8),
                IsHoliday = false // NO opera en feriados
            }
        },
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { Price = 100, ReserveTypeId = ReserveTypeIdEnum.Ida }
        }
        };

        var holidays = new List<Holiday>
    {
        new Holiday { HolidayDate = feriado, Description = "Feriado test" }
    };

        var reserves = new List<Reserve>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity([service]).Object);
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity([vehicle]).Object);
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves).Object);
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, dateProviderMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.DoesNotContain(reserves, r => r.ReserveDate.Date == feriado.Date);
    }


    [Fact]
    public async Task GenerateFutureReserves_ShouldCreateReserveOnHoliday_WhenServiceScheduleAllowsHoliday()
    {
        // Arrange
        var today = new DateTime(2025, 05, 12); // Lunes
        var feriado = today.AddDays(1); // Martes

        var dateProviderMock = new Mock<IDateTimeProvider>();
        dateProviderMock.Setup(x => x.UtcNow).Returns(today);

        var reserveOptionMock = new Mock<IReserveOption>();
        reserveOptionMock.Setup(x => x.ReserveGenerationDays).Returns(2);

        var vehicle = new Vehicle { VehicleId = 1, Status = EntityStatusEnum.Active };

        var service = new Service
        {
            ServiceId = 1,
            EstimatedDuration = TimeSpan.FromHours(2),
            VehicleId = vehicle.VehicleId,
            Vehicle = vehicle,
            OriginId = 1,
            Origin = new City() { CityId = 1, Name = "Origin City" },
            DestinationId = 2,
            Destination = new City() { CityId = 2, Name = "Destination City" },
            Schedules = new List<ServiceSchedule>
        {
            new ServiceSchedule
            {
                ServiceScheduleId = 1,
                StartDay = DayOfWeek.Monday,
                EndDay = DayOfWeek.Tuesday,
                DepartureHour = TimeSpan.FromHours(8),
                IsHoliday = true // Sí opera en feriados
            }
        },
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { Price = 100, ReserveTypeId = ReserveTypeIdEnum.Ida }
        }
        };

        var holidays = new List<Holiday>
    {
        new Holiday { HolidayDate = feriado, Description = "Feriado test" }
    };

        var reserves = new List<Reserve>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity([service]).Object);
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity([vehicle]).Object);
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves).Object);
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var serviceBusiness = new ServiceBusiness(_contextMock.Object, reserveOptionMock.Object, dateProviderMock.Object);

        // Act
        await serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.Contains(reserves, r => r.ReserveDate.Date == feriado.Date);
    }


    [Fact]
    public async Task GenerateFutureReserves_ShouldNotCreateReserves_WhenServiceHasNoPrices()
    {
        // Arrange
        var today = _dateTimeProviderMock.Object.UtcNow;
        var dayOfWeek = today.DayOfWeek;

        var vehicle = new Vehicle
        {
            VehicleId = 1,
            InternalNumber = "ABC123",
            Status = EntityStatusEnum.Active,
            AvailableQuantity = 10,
            VehicleType = new VehicleType { Name = "Bus", Quantity = 50 }
        };

        var service = new Service
        {
            ServiceId = 1,
            Name = "Servicio sin precios",
            EstimatedDuration = TimeSpan.FromHours(2),
            Vehicle = vehicle,
            VehicleId = vehicle.VehicleId,
            OriginId = 1,
            DestinationId = 2,
            ReservePrices = new List<ReservePrice>(), // Sin precios
            Schedules = new List<ServiceSchedule>
        {
            new ServiceSchedule
            {
                ServiceScheduleId = 1,
                StartDay = dayOfWeek,
                EndDay = dayOfWeek,
                DepartureHour = TimeSpan.FromHours(8),
                IsHoliday = false
            }
        }
        };

        var services = new List<Service> { service };
        var vehicles = new List<Vehicle> { vehicle };
        var reserves = new List<Reserve>();
        var holidays = new List<Holiday>();

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services).Object);
        _contextMock.Setup(x => x.Vehicles).Returns(GetMockDbSetWithIdentity(vehicles).Object);
        _contextMock.Setup(x => x.Reserves).Returns(GetMockDbSetWithIdentity(reserves).Object);
        _contextMock.Setup(x => x.Holidays).Returns(GetMockDbSetWithIdentity(holidays).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        // Act
        await _serviceBusiness.GenerateFutureReservesAsync();

        // Assert
        Assert.Empty(reserves);
    }

    [Fact]
    public async Task UpdatePricesByPercentageAsync_ShouldUpdatePrices_WhenValidRequestDto()
    {
        // Arrange
        var reserveOptionMock = new Mock<IReserveOption>();

        var requestDto = new PriceMassiveUpdateRequestDto(
            new List<PricePercentageUpdateDto>
            {
            new PricePercentageUpdateDto((int)ReserveTypeIdEnum.Ida, 10),
            new PricePercentageUpdateDto((int)ReserveTypeIdEnum.IdaVuelta, 20)
            });

        var service = new Service
        {
            ServiceId = 1,
            Name = "Servicio A",
            Origin = new City { CityId = 1, Name = "Ciudad A" },
            Destination = new City { CityId = 2, Name = "Ciudad B" },
            EstimatedDuration = TimeSpan.FromHours(2),
            Vehicle = new Vehicle
            {
                VehicleId = 1,
                InternalNumber = "ABC123",
                Status = EntityStatusEnum.Active,
                AvailableQuantity = 10,
                VehicleType = new VehicleType { Name = "Bus", Quantity = 50 }
            },
            ReservePrices = new List<ReservePrice>
        {
            new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.Ida, Status = EntityStatusEnum.Active, Price = 100m },
            new ReservePrice { ReserveTypeId = ReserveTypeIdEnum.IdaVuelta, Status = EntityStatusEnum.Active, Price = 200m }
        }
        };

        var services = new List<Service> { service };

        _contextMock.Setup(x => x.Services).Returns(GetMockDbSetWithIdentity(services).Object);

        SetupSaveChangesWithOutboxAsync(ContextMock);

        // Act

        var result = await _serviceBusiness.UpdatePricesByPercentageAsync(requestDto);

        var requestServiceReportDto = new PagedReportRequestDto<ServiceReportFilterRequestDto>
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = new ServiceReportFilterRequestDto("Servicio A", null, null, null, null, null),
            SortBy = "name",
            SortDescending = false
        };

        var serviceReportResult = await _serviceBusiness.GetServiceReport(requestServiceReportDto);

        // Assert
        Assert.True(serviceReportResult.IsSuccess);

        // Verificar que los precios actualizados estén en el reporte
        var reportService = serviceReportResult.Value.Items.FirstOrDefault();
        Assert.NotNull(reportService);

        var idaPriceReport = reportService.ReservePrices.FirstOrDefault(rp => rp.ReserveTypeId == (int)ReserveTypeIdEnum.Ida);
        var idaVueltaPriceReport = reportService.ReservePrices.FirstOrDefault(rp => rp.ReserveTypeId == (int)ReserveTypeIdEnum.IdaVuelta);

        Assert.NotNull(idaPriceReport);
        Assert.Equal(110m, idaPriceReport.Price);  // 100 + 10%

        Assert.NotNull(idaVueltaPriceReport);
        Assert.Equal(240m, idaVueltaPriceReport.Price);  // 200 + 20%
        Assert.True(result.IsSuccess);
        Assert.Equal(110m, service.ReservePrices.First(p => p.ReserveTypeId == ReserveTypeIdEnum.Ida).Price);  // 100 + 10%
        Assert.Equal(240m, service.ReservePrices.First(p => p.ReserveTypeId == ReserveTypeIdEnum.IdaVuelta).Price);  // 200 + 20%
    }

    [Fact]
    public async Task AddPrice_ShouldFail_WhenServiceNotFound()
    {
        // Arrange
        var services = new List<Service>(); // No hay servicios
        _contextMock.Setup(x => x.Services).Returns(GetQueryableMockDbSet(services).Object);

        var request = new ServicePriceAddDto((int)ReserveTypeIdEnum.Ida, 1000m);

        var result = await _serviceBusiness.AddPrice(1, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ServiceError.ServiceNotFound);
    }

    [Fact]
    public async Task AddPrice_ShouldFail_WhenReservePriceAlreadyExists()
    {
        // Arrange
        var existingPrice = new ReservePrice
        {
            ReserveTypeId = ReserveTypeIdEnum.Ida
        };

        var service = new Service
        {
            ServiceId = 1,
            ReservePrices = new List<ReservePrice> { existingPrice }
        };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);

        var request = new ServicePriceAddDto((int)ReserveTypeIdEnum.Ida, 1500m);

        var result = await _serviceBusiness.AddPrice(1, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReservePriceError.ReservePriceAlreadyExists);
    }

    [Fact]
    public async Task AddPrice_ShouldSucceed_WhenValidData()
    {
        // Arrange
        var serviceId = 1;
        var reservePrices = new List<ReservePrice>();

        var service = new Service
        {
            ServiceId = serviceId,
            ReservePrices = reservePrices
        };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);

        _contextMock.Setup(x => x.ReservePrices.Add(It.IsAny<ReservePrice>()));

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServicePriceAddDto((int)ReserveTypeIdEnum.Ida, 5000);

        // Act
        var result = await _serviceBusiness.AddPrice(serviceId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePrice_ShouldSucceed_WhenValidData()
    {
        // Arrange
        var reservePrice = new ReservePrice
        {
            ReservePriceId = 10,
            Price = 3000,
            Status = EntityStatusEnum.Active
        };

        _contextMock.Setup(x => x.ReservePrices.FindAsync(reservePrice.ReservePriceId))
            .ReturnsAsync(reservePrice);

        var service = new Service
        {
            ServiceId = 1
        };

        _contextMock.Setup(x => x.Services)
            .Returns(GetQueryableMockDbSet(new List<Service> { service }).Object);

        SetupSaveChangesWithOutboxAsync(_contextMock);

        var dto = new ServicePriceUpdateDto(10, 4500m);

        // Act
        var result = await _serviceBusiness.UpdatePrice(service.ServiceId, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        reservePrice.Price.Should().Be(4500);
    }


}
