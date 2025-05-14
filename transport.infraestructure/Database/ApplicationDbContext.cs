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

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default)
    {
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
