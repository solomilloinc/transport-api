using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace transport.persistance.EntityTypesConfigurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicle");
        builder.HasKey(v => v.VehicleId);
        builder.Property(v => v.InternalNumber).HasMaxLength(50).IsRequired();
        builder.HasOne(v => v.VehicleType)
               .WithMany()
               .HasForeignKey(v => v.VehicleTypeId)
               .IsRequired();
    }
}
