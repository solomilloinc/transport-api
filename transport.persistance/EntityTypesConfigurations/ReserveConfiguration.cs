using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace transport.persistance.EntityTypesConfigurations;

public class ReserveConfiguration : IEntityTypeConfiguration<Reserve>
{
    public void Configure(EntityTypeBuilder<Reserve> builder)
    {
        builder.ToTable("Reserve");
        builder.HasKey(r => r.ReserveId);
        builder.Property(r => r.ReserveDate).IsRequired();
        builder.Property(r => r.Status).IsRequired();
        builder.HasOne(r => r.Driver).WithMany().HasForeignKey(r => r.DriverId);
        builder.HasOne(r => r.Service).WithMany().HasForeignKey(r => r.ServiceId).IsRequired();
    }
}
