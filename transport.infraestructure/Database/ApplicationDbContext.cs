using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Transport.SharedKernel;
using Transport.Domain;
using Transport.Domain.CashBoxes;
using Transport.Domain.Drivers;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.Reserves;
using Transport.Domain.Users;
using Transport.Domain.Vehicles;
using Transport.Domain.Cities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Transport.Domain.Services;
using Transport.Business.Authentication;
using Transport.Domain.Directions;
using Transport.Domain.Passengers;
using Transport.Domain.Tenants;
using Transport.Domain.Trips;

namespace Transport.Infraestructure.Database;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Driver> Drivers { get; set; }

    public DbSet<Customer> Customers { get; set; }

    public DbSet<Reserve> Reserves { get; set; }

    public DbSet<Direction> Directions { get; set; }

    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<VehicleType> VehicleTypes { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Holiday> Holidays { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<TripPrice> TripPrices { get; set; }
    public DbSet<Passenger> Passengers { get; set; }
    public DbSet<ServiceSchedule> ServiceSchedules { get; set; }
    public DbSet<ReservePayment> ReservePayments { get; set; }
    public DbSet<ReserveSlotLock> ReserveSlotLocks { get; set; }
    public DbSet<CustomerAccountTransaction> CustomerAccountTransactions { get; set; }
    public DbSet<CashBox> CashBoxes { get; set; }
    public DbSet<ServiceDirection> ServiceDirections { get; set; }
    public DbSet<ServiceCustomer> ServiceCustomers { get; set; }
    public DbSet<ReserveDirection> ReserveDirections { get; set; }
    public DbSet<TripPickupStop> TripPickupStops { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConfig> TenantConfigs { get; set; }
    public DbSet<TenantPaymentConfig> TenantPaymentConfigs { get; set; }
    public DbSet<TenantReserveConfig> TenantReserveConfigs { get; set; }

    private readonly IUserContext _userContext;
    private readonly ITenantContext _tenantContext;

    // EF Core re-evaluates this on each query, enabling per-request tenant filtering
    private int CurrentTenantId => _tenantContext?.TenantId ?? 0;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IUserContext userContext, ITenantContext tenantContext)
        : base(options)
    {
        _userContext = userContext;
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IAuditable).IsAssignableFrom(entityType.ClrType))
            {
                var entity = modelBuilder.Entity(entityType.ClrType);

                entity.Property("CreatedBy").IsRequired().HasColumnType("VARCHAR(256)").HasDefaultValue("System");
                entity.Property("CreatedDate").IsRequired().HasDefaultValueSql("GETDATE()");
                entity.Property("UpdatedBy").HasColumnType("VARCHAR(256)");
                entity.Property("UpdatedDate");
            }

            // Configure TenantId FK + Global Query Filter for all ITenantScoped entities
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var entity = modelBuilder.Entity(entityType.ClrType);
                entity.Property("TenantId").IsRequired().HasDefaultValue(1);
                entity.HasIndex("TenantId");

                entity.HasOne(typeof(Tenant))
                    .WithMany()
                    .HasForeignKey("TenantId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                // Global Query Filter: e => e.TenantId == CurrentTenantId
                var clrType = entityType.ClrType;
                var parameter = Expression.Parameter(clrType, "e");
                var tenantIdProperty = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                var currentTenantId = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
                var filter = Expression.Lambda(Expression.Equal(tenantIdProperty, currentTenantId), parameter);
                entity.HasQueryFilter(filter);
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var username = _userContext?.Email?.ToString() ?? "System";

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedDate = now;
                entry.Entity.CreatedBy = username;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedDate = now;
                entry.Entity.UpdatedBy = username;
            }
        }

        // Auto-set TenantId on insert, prevent modification on update
        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.TenantId = CurrentTenantId;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(ITenantScoped.TenantId)).IsModified = false;
            }
        }

        var domainEvents = ChangeTracker.Entries<Entity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var entity in ChangeTracker.Entries<Entity>())
        {
            entity.Entity.ClearDomainEvents();
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        var outboxMessages = domainEvents.Select(e => e.ToOutboxMessage()).ToList();
        OutboxMessages.AddRange(outboxMessages);

        await base.SaveChangesAsync(cancellationToken);

        return result;
    }

    public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class
            => base.Entry(entity);
}
