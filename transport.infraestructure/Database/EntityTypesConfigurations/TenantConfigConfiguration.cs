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

        // Identity
        builder.Property(tc => tc.CompanyName).HasMaxLength(200);
        builder.Property(tc => tc.CompanyNameShort).HasMaxLength(100);
        builder.Property(tc => tc.CompanyNameLegal).HasMaxLength(300);
        builder.Property(tc => tc.LogoUrl).HasMaxLength(500);
        builder.Property(tc => tc.FaviconUrl).HasMaxLength(500);
        builder.Property(tc => tc.Tagline).HasMaxLength(500);

        // Contact
        builder.Property(tc => tc.ContactAddress).HasMaxLength(300);
        builder.Property(tc => tc.ContactPhone).HasMaxLength(50);
        builder.Property(tc => tc.ContactEmail).HasMaxLength(200);
        builder.Property(tc => tc.BookingsEmail).HasMaxLength(200);

        // Legal
        builder.Property(tc => tc.TermsText).HasColumnType("nvarchar(max)");
        builder.Property(tc => tc.CancellationPolicy).HasColumnType("nvarchar(max)");

        // Style JSON (theme, typography, images, landing, seo, contact.schedule)
        builder.Property(tc => tc.StyleConfigJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(tc => tc.TenantId).IsUnique();

        builder.HasOne(tc => tc.Tenant)
            .WithOne(t => t.Config)
            .HasForeignKey<TenantConfig>(tc => tc.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
