﻿using Microsoft.EntityFrameworkCore;
using transport.common;
using transport.domain;
using transport.domain.Drivers;
using Transport.Business.Data;

namespace transport.infraestructure.Database;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Driver> Drivers { get; set; }

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
