using Microsoft.EntityFrameworkCore;
using transport.domain;
using transport.domain.Drivers;

namespace Transport.Business.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Driver> Drivers { get; }
    Task<int> SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default);
}
