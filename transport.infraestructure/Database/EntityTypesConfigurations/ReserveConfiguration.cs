using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Reserves;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class ReserveConfiguration : IEntityTypeConfiguration<Reserve>
{
    public void Configure(EntityTypeBuilder<Reserve> builder)
    {
        builder.ToTable("Reserve");
        builder.HasKey(r => r.ReserveId);
        builder.Property(r => r.ReserveDate).IsRequired();
        builder.Property(r => r.Status).IsRequired();

        builder.HasOne(r => r.Service)
                   .WithMany(s => s.Reserves)
                   .HasForeignKey(r => r.ServiceId)
                   .IsRequired();

        builder.HasOne(r => r.Driver)
               .WithMany(d => d.Reserves)
               .HasForeignKey(r => r.DriverId);

        builder.HasOne(r => r.Service)
                 .WithMany(s => s.Reserves)
                 .HasForeignKey(r => r.ServiceId)
                 .IsRequired();

        builder.HasOne(r => r.Vehicle)
            .WithMany()
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.Status)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        builder.HasIndex(r => new { r.ServiceId, r.ReserveDate });

        builder.HasIndex(r => new { r.Status, r.ReserveDate });
    }
}
