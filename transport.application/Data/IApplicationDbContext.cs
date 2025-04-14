using Microsoft.EntityFrameworkCore;
using Transport.Domain;
using Transport.Domain.Drivers;
using Transport.Domain.Customers;
using Transport.Domain.Reserves;
using Transport.Domain.Users;

namespace Transport.Business.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Reserve> Reserves { get; }
    DbSet<Direction> Directions { get; }
    Task<int> SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default);
}
