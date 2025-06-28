using Microsoft.EntityFrameworkCore;
using Transport.Domain;
using Transport.Domain.Drivers;
using Transport.Domain.Customers;
using Transport.Domain.Reserves;
using Transport.Domain.Users;
using Transport.Domain.Vehicles;
using Transport.Domain.Cities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Transport.Domain.Services;
using Transport.Domain.Directions;

namespace Transport.Business.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Reserve> Reserves { get; }
    DbSet<Direction> Directions { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<VehicleType> VehicleTypes { get; }
    DbSet<City> Cities { get; }
    DbSet<Service> Services { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<ReservePrice> ReservePrices { get; }
    DbSet<CustomerReserve> CustomerReserves { get; }
    DbSet<ServiceSchedule> ServiceSchedules { get; }
    DbSet<ReservePayment> ReservePayments { get; }
    DbSet<CustomerAccountTransaction> CustomerAccountTransactions { get; }
    Task<int> SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default);
}
