using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Trips;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class TripPriceConfiguration : IEntityTypeConfiguration<TripPrice>
{
    public void Configure(EntityTypeBuilder<TripPrice> builder)
    {
        builder.ToTable("TripPrice");

        builder.HasKey(tp => tp.TripPriceId);

        builder.Property(tp => tp.Price)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(tp => tp.Order)
            .IsRequired();

        builder.Property(tp => tp.ReserveTypeId)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(tp => tp.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(tp => tp.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(tp => tp.UpdatedBy)
            .HasMaxLength(100);

        // Relación con Trip
        builder.HasOne(tp => tp.Trip)
            .WithMany(t => t.Prices)
            .HasForeignKey(tp => tp.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relación con City
        builder.HasOne(tp => tp.City)
            .WithMany()
            .HasForeignKey(tp => tp.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relación con Direction (opcional)
        builder.HasOne(tp => tp.Direction)
            .WithMany()
            .HasForeignKey(tp => tp.DirectionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único: un precio por Trip + City + Direction + ReserveType
        builder.HasIndex(tp => new { tp.TripId, tp.CityId, tp.DirectionId, tp.ReserveTypeId })
            .HasFilter("[Status] = 'Active'")
            .IsUnique();
    }
}
