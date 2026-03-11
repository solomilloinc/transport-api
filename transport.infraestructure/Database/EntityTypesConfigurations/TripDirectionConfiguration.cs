using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Trips;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class TripDirectionConfiguration : IEntityTypeConfiguration<TripDirection>
{
    public void Configure(EntityTypeBuilder<TripDirection> builder)
    {
        builder.ToTable("TripDirection");

        builder.HasKey(td => td.TripDirectionId);

        builder.Property(td => td.Order)
            .IsRequired();

        builder.Property(td => td.PickupTimeOffset)
            .IsRequired();

        builder.Property(td => td.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(td => td.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(td => td.UpdatedBy)
            .HasMaxLength(100);

        // Relación con Trip
        builder.HasOne(td => td.Trip)
            .WithMany(t => t.Directions)
            .HasForeignKey(td => td.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relación con Direction
        builder.HasOne(td => td.Direction)
            .WithMany()
            .HasForeignKey(td => td.DirectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único: una dirección por Trip (solo activos)
        builder.HasIndex(td => new { td.TripId, td.DirectionId })
            .HasFilter("[Status] = 'Active'")
            .IsUnique();
    }
}
