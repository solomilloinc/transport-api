using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace transport.infraestructure.Database.EntityTypesConfigurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");
        builder.HasKey(s => s.ServiceId);
        builder.Property(s => s.Name).HasMaxLength(250).IsRequired();
        builder.Property(s => s.DayStart).IsRequired();
        builder.Property(s => s.DayEnd).IsRequired();
        builder.Property(s => s.EstimatedDuration).IsRequired();
        builder.Property(s => s.DepartureHour).IsRequired();
        builder.Property(s => s.IsHoliday).IsRequired();
        builder.Property(s => s.Status).IsRequired();
        builder.HasOne(s => s.Origin)
               .WithMany()
               .HasForeignKey(s => s.OriginId);
        builder.HasOne(s => s.Destination)
               .WithMany()
               .HasForeignKey(s => s.DestinationId);
    }
}
