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
        builder.Property(s => s.DayOfWeek).IsRequired();
        builder.Property(s => s.DepartureHour).IsRequired();
        builder.Property(s => s.IsHoliday).IsRequired();
        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasColumnType("VARCHAR(20)");

        builder.HasOne(s => s.Trip)
            .WithMany()
            .HasForeignKey(s => s.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.TenantId, s.TripId, s.DayOfWeek, s.DepartureHour })
            .HasFilter("[Status] = 'Active'")
            .IsUnique();
    }
}
