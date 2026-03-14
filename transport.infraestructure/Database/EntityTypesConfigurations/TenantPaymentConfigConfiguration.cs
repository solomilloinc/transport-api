using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Tenants;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class TenantPaymentConfigConfiguration : IEntityTypeConfiguration<TenantPaymentConfig>
{
    public void Configure(EntityTypeBuilder<TenantPaymentConfig> builder)
    {
        builder.ToTable("TenantPaymentConfig");
        builder.HasKey(tpc => tpc.TenantPaymentConfigId);

        builder.Property(tpc => tpc.AccessToken)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(tpc => tpc.PublicKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(tpc => tpc.Status).IsRequired();

        builder.HasIndex(tpc => tpc.TenantId).IsUnique();

        builder.HasOne(tpc => tpc.Tenant)
            .WithOne(t => t.PaymentConfig)
            .HasForeignKey<TenantPaymentConfig>(tpc => tpc.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
