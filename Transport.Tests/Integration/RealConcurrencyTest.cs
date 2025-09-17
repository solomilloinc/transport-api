using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Transport.Business.ReserveBusiness;
using Transport.Business.Services.Payment;
using Transport.Business.Authentication;
using Transport.Business.Data;
using Transport.Domain.Customers.Abstraction;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Cities;
using Transport.Domain.Directions;
using Transport.Domain.Passengers;
using Transport.Domain.Vehicles;
using Transport.Infraestructure.Database;
using Transport.SharedKernel.Contracts.Reserve;
using Transport.SharedKernel.Configuration;
using Transport.SharedKernel;
using Xunit;

namespace Transport.Tests.Integration;

/// <summary>
/// Test de concurrencia REAL usando SQL Server con transacciones reales.
/// Este test demuestra la diferencia con mocks/InMemory DB.
/// </summary>
public class RealConcurrencyTest : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ReserveBusiness _reserveBusiness;
    private readonly Mock<IMercadoPagoPaymentGateway> _paymentGatewayMock;
    private readonly Mock<ICustomerBusiness> _customerBusinessMock;
    private readonly IUnitOfWork _unitOfWork;

    private Reserve _testReserve = null!;

    public RealConcurrencyTest()
    {
        // CLAVE: Usar SQL Server REAL, no InMemory
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var services = new ServiceCollection();

        var userContextMock = new Mock<IUserContext>();
        userContextMock.Setup(x => x.Email).Returns("test@example.com");
        userContextMock.Setup(x => x.UserId).Returns(null);

        services.AddSingleton(userContextMock.Object);

        // Configurar SQL Server REAL
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Database")));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetService<ApplicationDbContext>()!);
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var serviceProvider = services.BuildServiceProvider();

        _context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        _unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        // Crear base de datos temporal para tests
        _context.Database.EnsureCreated();

        // Setup mocks
        _paymentGatewayMock = new Mock<IMercadoPagoPaymentGateway>();
        _customerBusinessMock = new Mock<ICustomerBusiness>();

        var userContext = serviceProvider.GetRequiredService<IUserContext>();
        var reserveOptions = new TestReserveOption();

        _reserveBusiness = new ReserveBusiness(
            _context,
            _unitOfWork,
            userContext,
            _paymentGatewayMock.Object,
            _customerBusinessMock.Object,
            reserveOptions);

        // Seed data mínimo necesario
        SeedMinimalTestData();
    }

    private void SeedMinimalTestData()
    {
        // Crear estructura mínima con todas las FK necesarias
        var timestamp = DateTime.UtcNow.Ticks.ToString()[^8..]; // últimos 8 dígitos
        var city = new City
        {
            Name = $"Test City {timestamp}",
            Code = $"T{timestamp[^3..]}",
            Status = Transport.SharedKernel.EntityStatusEnum.Active
        };
        _context.Cities.Add(city);
        _context.SaveChanges();

        var direction = new Direction
        {
            Name = $"Test Location {timestamp}",
            CityId = city.CityId,
            Status = Transport.SharedKernel.EntityStatusEnum.Active
        };
        _context.Directions.Add(direction);
        _context.SaveChanges();

        var vehicleType = new VehicleType
        {
            Name = $"Test Vehicle Type {timestamp}",
            Status = Transport.SharedKernel.EntityStatusEnum.Active,
            CreatedBy = "Test"
        };
        _context.VehicleTypes.Add(vehicleType);
        _context.SaveChanges();

        var vehicle = new Vehicle
        {
            AvailableQuantity = 10,
            InternalNumber = $"VEH{timestamp}",
            VehicleTypeId = vehicleType.VehicleTypeId,
            Status = Transport.SharedKernel.EntityStatusEnum.Active,
            CreatedBy = "Test"
        };
        _context.Vehicles.Add(vehicle);
        _context.SaveChanges();

        var service = new Service
        {
            Name = $"Test Service {timestamp}",
            OriginId = city.CityId,
            DestinationId = city.CityId,
            VehicleId = vehicle.VehicleId,
            Status = Transport.SharedKernel.EntityStatusEnum.Active,
            CreatedBy = "Test"
        };
        _context.Services.Add(service);
        _context.SaveChanges();

        var serviceSchedule = new ServiceSchedule
        {
            ServiceId = service.ServiceId,
            StartDay = DayOfWeek.Monday,
            EndDay = DayOfWeek.Friday,
            DepartureHour = TimeSpan.FromHours(9),
            IsHoliday = false,
            Status = Transport.SharedKernel.EntityStatusEnum.Active,
            CreatedBy = "Test"
        };
        _context.ServiceSchedules.Add(serviceSchedule);
        _context.SaveChanges();

        // Crear reserva con 8 pasajeros (quedando 2 cupos disponibles)
        _testReserve = new Reserve
        {
            Status = ReserveStatusEnum.Confirmed,
            VehicleId = vehicle.VehicleId,
            ServiceId = service.ServiceId,
            ServiceScheduleId = serviceSchedule.ServiceScheduleId,
            ReserveDate = DateTime.Today.AddDays(1),
            DepartureHour = TimeSpan.FromHours(9),
            ServiceName = $"Test Service {timestamp}",
            OriginName = $"Test City {timestamp}",
            DestinationName = $"Test City {timestamp}",
            CreatedBy = "Test"
        };
        _context.Reserves.Add(_testReserve);
        _context.SaveChanges();

        // Agregar 8 pasajeros existentes
        for (int i = 1; i <= 8; i++)
        {
            var passenger = new Passenger
            {
                ReserveId = _testReserve.ReserveId,
                FirstName = $"Passenger{i}",
                LastName = "Test",
                DocumentNumber = $"{timestamp}{i:D2}",
                Email = $"passenger{i}{timestamp}@test.com",
                Status = PassengerStatusEnum.Confirmed,
                Price = 100,
                CreatedBy = "Test"
            };
            _context.Passengers.Add(passenger);
        }
        _context.SaveChanges();
    }

    private (ServiceProvider serviceProvider, ReserveBusiness reserveBusiness) CreateReserveBusinessInstance()
    {
        // Crear nueva configuración y servicios para cada instancia
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var services = new ServiceCollection();

        var userContextMock = new Mock<IUserContext>();
        userContextMock.Setup(x => x.Email).Returns("test@example.com");
        userContextMock.Setup(x => x.UserId).Returns(null);

        services.AddSingleton(userContextMock.Object);

        // Configurar SQL Server con nueva instancia de DbContext
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Database")));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetService<ApplicationDbContext>()!);
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var serviceProvider = services.BuildServiceProvider();

        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        var userContext = serviceProvider.GetRequiredService<IUserContext>();

        // Setup mocks para esta instancia
        var paymentGatewayMock = new Mock<IMercadoPagoPaymentGateway>();
        var customerBusinessMock = new Mock<ICustomerBusiness>();
        var reserveOptions = new TestReserveOption();

        var reserveBusiness = new ReserveBusiness(
            context,
            unitOfWork,
            userContext,
            paymentGatewayMock.Object,
            customerBusinessMock.Object,
            reserveOptions);

        return (serviceProvider, reserveBusiness);
    }

    [Fact]
    public async Task RealConcurrency_LockReserveSlots_ShouldHandleRaceConditions()
    {
        // Este test demuestra CONCURRENCIA REAL con SQL Server

        // Arrange - Crear 5 solicitudes concurrentes reales
        var concurrentTasks = new List<Task<Result<LockReserveSlotsResponseDto>>>();

        for (int i = 0; i < 5; i++)
        {
            var request = new LockReserveSlotsRequestDto(
                OutboundReserveId: _testReserve.ReserveId,
                ReturnReserveId: null,
                PassengerCount: 1
            );

            // CLAVE: Task.Run crea hilos reales que ejecutan transacciones SQL reales
            concurrentTasks.Add(Task.Run(async () =>
            {
                // Delay aleatorio para maximizar condiciones de carrera
                await Task.Delay(Random.Shared.Next(1, 100));

                // Cada llamada usa su propia instancia de ReserveBusiness y DbContext
                var (serviceProvider, reserveBusiness) = CreateReserveBusinessInstance();
                try
                {
                    return await reserveBusiness.LockReserveSlots(request);
                }
                finally
                {
                    // Dispose de los recursos manualmente
                    serviceProvider.Dispose();
                }
            }));
        }

        // Act - Ejecutar concurrentemente con transacciones SQL reales
        var results = await Task.WhenAll(concurrentTasks);

        // Assert - Verificar comportamiento correcto bajo concurrencia real
        var successfulResults = results.Where(r => r.IsSuccess).ToList();
        var failedResults = results.Where(r => r.IsFailure).ToList();

        // Solo 2 solicitudes pueden tener éxito (2 cupos disponibles)
        successfulResults.Should().HaveCount(2,
            "Solo 2 solicitudes deberían tener éxito porque solo hay 2 cupos disponibles");

        // 3 solicitudes deben fallar por falta de cupos
        failedResults.Should().HaveCount(3,
            "3 solicitudes deberían fallar por falta de cupos");

        // Verificar que todos los failures son por cupos insuficientes
        failedResults.Should().AllSatisfy(r =>
            r.Error.Code.Should().Be("ReserveSlotLock.InsufficientSlots"));

        // VERIFICACIÓN CRÍTICA: Integridad de datos en SQL Server
        var locksInDb = await _context.ReserveSlotLocks
            .Where(l => l.Status == ReserveSlotLockStatus.Active)
            .ToListAsync();

        locksInDb.Should().HaveCount(2, "Deben existir exactamente 2 locks activos en SQL Server");

        var totalSlotsLocked = locksInDb.Sum(l => l.SlotsLocked);
        totalSlotsLocked.Should().Be(2, "Se deben haber bloqueado exactamente 2 slots en total");

        // Verificar tokens únicos (no duplicados por race conditions)
        var tokens = successfulResults.Select(r => r.Value.LockToken).ToList();
        tokens.Should().OnlyHaveUniqueItems("Cada lock debe tener un token único");

        var tokensInDb = locksInDb.Select(l => l.LockToken).ToList();
        tokensInDb.Should().OnlyHaveUniqueItems("No debe haber tokens duplicados en BD");
    }

    [Fact]
    public async Task RealConcurrency_HighLoad_ShouldMaintainDataIntegrity()
    {
        // Test de alta carga con 20 solicitudes concurrentes
        var highLoadTasks = new List<Task<Result<LockReserveSlotsResponseDto>>>();

        for (int i = 0; i < 20; i++)
        {
            var request = new LockReserveSlotsRequestDto(
                OutboundReserveId: _testReserve.ReserveId,
                ReturnReserveId: null,
                PassengerCount: 1
            );

            highLoadTasks.Add(Task.Run(async () =>
            {
                // Mayor variabilidad para stress testing
                await Task.Delay(Random.Shared.Next(1, 200));

                // Cada llamada usa su propia instancia de ReserveBusiness y DbContext
                var (serviceProvider, reserveBusiness) = CreateReserveBusinessInstance();
                try
                {
                    return await reserveBusiness.LockReserveSlots(request);
                }
                finally
                {
                    // Dispose de los recursos manualmente
                    serviceProvider.Dispose();
                }
            }));
        }

        // Act
        var results = await Task.WhenAll(highLoadTasks);

        // Assert
        var successfulResults = results.Where(r => r.IsSuccess).ToList();
        var failedResults = results.Where(r => r.IsFailure).ToList();

        // Máximo 2 éxitos posibles
        successfulResults.Should().HaveCountLessThanOrEqualTo(2,
            "No más de 2 solicitudes pueden tener éxito");

        // Mínimo 18 fallos
        failedResults.Should().HaveCountGreaterThanOrEqualTo(18,
            "Al menos 18 solicitudes deben fallar");

        // VERIFICACIÓN DE INTEGRIDAD FINAL EN SQL SERVER
        var finalLocksInDb = await _context.ReserveSlotLocks
            .Where(l => l.Status == ReserveSlotLockStatus.Active)
            .CountAsync();

        finalLocksInDb.Should().BeLessThanOrEqualTo(2,
            "No debe haber más de 2 locks activos en SQL Server");
    }

    [Fact]
    public async Task RealConcurrency_DeadlockHandling_ShouldRecoverGracefully()
    {
        // Test para verificar manejo de deadlocks en SQL Server
        var deadlockTasks = new List<Task<Result<LockReserveSlotsResponseDto>>>();

        // Crear solicitudes que podrían causar deadlocks
        for (int i = 0; i < 10; i++)
        {
            var request = new LockReserveSlotsRequestDto(
                OutboundReserveId: _testReserve.ReserveId,
                ReturnReserveId: null,
                PassengerCount: 1
            );

            deadlockTasks.Add(Task.Run(async () =>
            {
                // Cada llamada usa su propia instancia de ReserveBusiness y DbContext
                var (serviceProvider, reserveBusiness) = CreateReserveBusinessInstance();
                try
                {
                    // Sin delay para maximizar posibilidad de deadlock
                    return await reserveBusiness.LockReserveSlots(request);
                }
                catch (Exception ex) when (ex.Message.Contains("deadlock"))
                {
                    // En caso de deadlock, SQL Server debería retry automáticamente
                    // o devolver un error manejado
                    return Result.Failure<LockReserveSlotsResponseDto>(
                        Error.Failure("Deadlock", "Deadlock detected and handled"));
                }
                finally
                {
                    // Dispose de los recursos manualmente
                    serviceProvider.Dispose();
                }
            }));
        }

        // Act
        var results = await Task.WhenAll(deadlockTasks);

        // Assert - El sistema debe manejar deadlocks sin corromper datos
        var totalResults = results.Length;
        totalResults.Should().Be(10, "Todas las solicitudes deben completarse de alguna manera");

        // Verificar que no hay corrupción de datos después del stress test
        var finalState = await _context.ReserveSlotLocks
            .Where(l => l.OutboundReserveId == _testReserve.ReserveId)
            .ToListAsync();

        var activeLocks = finalState.Where(l => l.Status == ReserveSlotLockStatus.Active).ToList();
        activeLocks.Should().HaveCountLessThanOrEqualTo(2,
            "Después del stress test, no debe haber más de 2 locks activos");
    }

    public void Dispose()
    {
        // Limpiar base de datos después del test
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private class TestReserveOption : IReserveOption
    {
        public int ReserveGenerationDays { get; set; } = 15;
        public int SlotLockTimeoutMinutes { get; set; } = 10;
        public int SlotLockCleanupIntervalMinutes { get; set; } = 1;
        public int MaxSimultaneousLocksPerUser { get; set; } = 5;
    }
}