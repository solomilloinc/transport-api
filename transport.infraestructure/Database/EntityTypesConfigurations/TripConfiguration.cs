using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Trips;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trip");

        builder.HasKey(t => t.TripId);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(t => t.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.UpdatedBy)
            .HasMaxLength(100);

        // Relación con OriginCity
        builder.HasOne(t => t.OriginCity)
            .WithMany()
            .HasForeignKey(t => t.OriginCityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relación con DestinationCity
        builder.HasOne(t => t.DestinationCity)
            .WithMany()
            .HasForeignKey(t => t.DestinationCityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único para evitar trips duplicados
        builder.HasIndex(t => new { t.OriginCityId, t.DestinationCityId })
            .HasFilter("[Status] = 'Active'")
            .IsUnique();
    }
}
