using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Services;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Service");
        builder.HasKey(s => s.ServiceId);
        builder.Property(s => s.Name).HasMaxLength(250).IsRequired();
        builder.Property(s => s.EstimatedDuration).IsRequired();
        builder.Property(s => s.StartDay).IsRequired();
        builder.Property(s => s.EndDay).IsRequired();
        builder.Property(s => s.Status).IsRequired();

        builder.HasOne(s => s.Origin)
            .WithMany(c => c.OriginServices)
            .HasForeignKey(s => s.OriginId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Destination)
            .WithMany(c => c.DestinationServices)
            .HasForeignKey(s => s.DestinationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
