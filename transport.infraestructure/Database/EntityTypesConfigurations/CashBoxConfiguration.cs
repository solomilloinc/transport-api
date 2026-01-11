using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.CashBoxes;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

internal class CashBoxConfiguration : IEntityTypeConfiguration<CashBox>
{
    public void Configure(EntityTypeBuilder<CashBox> builder)
    {
        builder.ToTable(nameof(CashBox));

        builder.HasKey(c => c.CashBoxId);

        builder.Property(c => c.Description)
               .HasColumnType("NVARCHAR(200)")
               .IsRequired(false);

        builder.Property(c => c.OpenedAt)
               .IsRequired();

        builder.Property(c => c.ClosedAt)
               .IsRequired(false);

        builder.Property(c => c.Status)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        // Relaciones
        builder.HasOne(c => c.OpenedByUser)
               .WithMany()
               .HasForeignKey(c => c.OpenedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ClosedByUser)
               .WithMany()
               .HasForeignKey(c => c.ClosedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Reserve)
               .WithMany()
               .HasForeignKey(c => c.ReserveId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Payments)
               .WithOne(p => p.CashBox)
               .HasForeignKey(p => p.CashBoxId)
               .OnDelete(DeleteBehavior.Restrict);

        // Indices
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.OpenedAt);
        builder.HasIndex(c => new { c.Status, c.OpenedAt });
    }
}
