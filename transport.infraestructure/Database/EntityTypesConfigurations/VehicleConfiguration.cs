using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Vehicles;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicle");
        builder.HasKey(v => v.VehicleId);
        builder.Property(v => v.InternalNumber).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Status).IsRequired();

        builder.HasOne(v => v.VehicleType)
       .WithMany(vt => vt.Vehicles)
       .HasForeignKey(v => v.VehicleTypeId)
       .IsRequired();
    }
}
