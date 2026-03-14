using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Tenants;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class TenantConfigConfiguration : IEntityTypeConfiguration<TenantConfig>
{
    public void Configure(EntityTypeBuilder<TenantConfig> builder)
    {
        builder.ToTable("TenantConfig");
        builder.HasKey(tc => tc.TenantConfigId);

        builder.Property(tc => tc.ConfigJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(tc => tc.TenantId).IsUnique();

        builder.HasOne(tc => tc.Tenant)
            .WithOne(t => t.Config)
            .HasForeignKey<TenantConfig>(tc => tc.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
