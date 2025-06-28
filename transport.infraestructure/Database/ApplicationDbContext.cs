using Microsoft.EntityFrameworkCore;
using Transport.SharedKernel;
using Transport.Domain;
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
    public DbSet<ReservePrice> ReservePrices { get; set; }
    public DbSet<CustomerReserve> CustomerReserves { get; set; }
    public DbSet<ServiceSchedule> ServiceSchedules { get; set; }
    public DbSet<ReservePayment> ReservePayments { get; set; }
    public DbSet<CustomerAccountTransaction> CustomerAccountTransactions { get; set; }

    private readonly IUserContext _userContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IUserContext userContext)
        : base(options)
    {
        _userContext = userContext;
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

}
