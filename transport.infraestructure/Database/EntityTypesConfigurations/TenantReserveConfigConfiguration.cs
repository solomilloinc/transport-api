using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Tenants;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class TenantReserveConfigConfiguration : IEntityTypeConfiguration<TenantReserveConfig>
{
    public void Configure(EntityTypeBuilder<TenantReserveConfig> builder)
    {
        builder.ToTable("TenantReserveConfig");
        builder.HasKey(trc => trc.TenantReserveConfigId);

        builder.Property(trc => trc.RoundTripSameDayOnly)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(trc => trc.TenantId).IsUnique();

        builder.HasOne(trc => trc.Tenant)
            .WithOne(t => t.ReserveConfig)
            .HasForeignKey<TenantReserveConfig>(trc => trc.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
