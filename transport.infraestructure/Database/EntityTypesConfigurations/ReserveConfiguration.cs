using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.Domain.Vehicles;
using Transport.Domain.Drivers;
using Transport.Domain.Customers;
using Transport.Domain.Users;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class ReserveConfiguration : IEntityTypeConfiguration<Reserve>
{
    public void Configure(EntityTypeBuilder<Reserve> builder)
    {
        builder.ToTable("Reserve");

        builder.HasKey(r => r.ReserveId);

        builder.Property(r => r.ReserveDate).IsRequired();

        builder.Property(r => r.Status)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        builder.Property(r => r.ServiceName)
               .HasColumnType("VARCHAR(250)")
               .IsRequired();

        builder.Property(r => r.OriginName)
               .HasColumnType("VARCHAR(100)")
               .IsRequired();

        builder.Property(r => r.DestinationName)
               .HasColumnType("VARCHAR(100)")
               .IsRequired();

        builder.Property(r => r.DepartureHour).IsRequired();
        builder.Property(r => r.IsHoliday).IsRequired();

        builder.HasOne(r => r.Service)
               .WithMany(s => s.Reserves)
               .HasForeignKey(r => r.ServiceId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Vehicle)
               .WithMany()
               .HasForeignKey(r => r.VehicleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Driver)
               .WithMany(d => d.Reserves)
               .HasForeignKey(r => r.DriverId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.ServiceSchedule)
               .WithMany()
               .HasForeignKey(r => r.ServiceScheduleId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Restrict);

        // Pasajeros
        builder.HasMany(r => r.Passengers)
               .WithOne(p => p.Reserve)
               .HasForeignKey(p => p.ReserveId)
               .OnDelete(DeleteBehavior.NoAction);

        // Índices
        builder.HasIndex(r => new { r.ServiceId, r.ReserveDate });
        builder.HasIndex(r => new { r.Status, r.ReserveDate });
    }
}
