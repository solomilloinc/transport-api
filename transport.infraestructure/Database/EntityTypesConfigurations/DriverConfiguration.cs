using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain.Drivers;

namespace transport.infraestructure.Database.EntityTypesConfigurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Driver");
        builder.HasKey(d => d.DriverId);
        builder.Property(d => d.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.LastName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.DocumentNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(d => d.DocumentNumber).IsUnique();
    }
}
