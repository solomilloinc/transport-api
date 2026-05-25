using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.FrequentSubscriptions;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class FrequentSubscriptionConfiguration : IEntityTypeConfiguration<FrequentSubscription>
{
    public void Configure(EntityTypeBuilder<FrequentSubscription> builder)
    {
        builder.ToTable("FrequentSubscription");

        builder.HasKey(s => s.FrequentSubscriptionId);

        builder.Property(s => s.ReserveTypeId)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        builder.Property(s => s.Status)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        builder.Property(s => s.StartDate)
               .HasColumnType("date")
               .IsRequired();

        builder.Property(s => s.EndDate)
               .HasColumnType("date")
               .IsRequired(false);

        builder.HasOne(s => s.Customer)
               .WithMany(c => c.FrequentSubscriptions)
               .HasForeignKey(s => s.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.OutboundService)
               .WithMany(svc => svc.OutboundSubscriptions)
               .HasForeignKey(s => s.OutboundServiceId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.InboundService)
               .WithMany(svc => svc.InboundSubscriptions)
               .HasForeignKey(s => s.InboundServiceId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.OutboundPickupLocation)
               .WithMany()
               .HasForeignKey(s => s.OutboundPickupLocationId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.OutboundDropoffLocation)
               .WithMany()
               .HasForeignKey(s => s.OutboundDropoffLocationId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.InboundPickupLocation)
               .WithMany()
               .HasForeignKey(s => s.InboundPickupLocationId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.InboundDropoffLocation)
               .WithMany()
               .HasForeignKey(s => s.InboundDropoffLocationId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);

        // Una sola suscripción activa por (Tenant, Customer, OutboundService) — evita duplicar.
        builder.HasIndex(s => new { s.TenantId, s.CustomerId, s.OutboundServiceId })
               .HasFilter("[Status] = 'Active'")
               .IsUnique();

        // Para queries del batch (subs activas por service como outbound o inbound).
        builder.HasIndex(s => new { s.TenantId, s.OutboundServiceId, s.Status });
        builder.HasIndex(s => new { s.TenantId, s.InboundServiceId, s.Status });
    }
}
