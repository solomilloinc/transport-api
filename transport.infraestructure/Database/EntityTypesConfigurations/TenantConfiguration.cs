using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Tenants;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenant");
        builder.HasKey(t => t.TenantId);

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(t => t.Code).IsUnique();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Domain)
            .HasMaxLength(253);

        builder.HasIndex(t => t.Domain)
            .IsUnique()
            .HasFilter("[Domain] IS NOT NULL");

        builder.Property(t => t.Status).IsRequired();
    }
}
