using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace Transport.Business.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    Task<int> SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default);
}
