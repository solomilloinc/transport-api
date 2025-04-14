using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class VehicleTypeConfiguration : IEntityTypeConfiguration<VehicleType>
{
    public void Configure(EntityTypeBuilder<VehicleType> builder)
    {
        builder.ToTable("VehicleType");
        builder.HasKey(vt => vt.VehicleTypeId);
        builder.Property(vt => vt.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(vt => vt.Name).IsUnique();
        builder.Property(vt => vt.Quantity).IsRequired();
        builder.Property(vt => vt.ImageBase64).HasColumnType("text");
    }
}
