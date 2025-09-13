using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Reserves;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class ReserveSlotLockConfiguration : IEntityTypeConfiguration<ReserveSlotLock>
{
    public void Configure(EntityTypeBuilder<ReserveSlotLock> builder)
    {
        builder.ToTable("ReserveSlotLock");

        builder.HasKey(r => r.ReserveSlotLockId);

        builder.Property(r => r.LockToken)
               .HasColumnType("VARCHAR(50)")
               .IsRequired();

        builder.Property(r => r.SlotsLocked).IsRequired();

        builder.Property(r => r.ExpiresAt).IsRequired();

        builder.Property(r => r.Status)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        builder.Property(r => r.UserEmail)
               .HasColumnType("VARCHAR(100)");

        builder.Property(r => r.UserDocumentNumber)
               .HasColumnType("VARCHAR(20)");

        builder.Property(r => r.CreatedBy)
               .HasColumnType("VARCHAR(100)")
               .IsRequired();

        builder.Property(r => r.UpdatedBy)
               .HasColumnType("VARCHAR(100)");

        builder.Property(r => r.CreatedDate).IsRequired();

        // Relaciones
        builder.HasOne(r => r.OutboundReserve)
               .WithMany()
               .HasForeignKey(r => r.OutboundReserveId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReturnReserve)
               .WithMany()
               .HasForeignKey(r => r.ReturnReserveId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Customer)
               .WithMany()
               .HasForeignKey(r => r.CustomerId)
               .OnDelete(DeleteBehavior.SetNull);

        // Índices para performance
        builder.HasIndex(r => r.LockToken).IsUnique();
        builder.HasIndex(r => new { r.Status, r.ExpiresAt });
        builder.HasIndex(r => r.OutboundReserveId);
        builder.HasIndex(r => r.ReturnReserveId);
        builder.HasIndex(r => r.CreatedDate);
    }
}