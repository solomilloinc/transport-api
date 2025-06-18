using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Reserves;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

internal class ReservePaymentConfiguration : IEntityTypeConfiguration<ReservePayment>
{
    public void Configure(EntityTypeBuilder<ReservePayment> builder)
    {
        builder.ToTable(nameof(ReservePayment));
        builder.HasKey(p => p.ReservePaymentId);

        builder.Property(p => p.Amount)
            .HasColumnType("decimal(18,2)");

        builder.Property(r => r.Method)
            .HasConversion<string>()
            .HasColumnType("VARCHAR(20)")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasColumnType("VARCHAR(20)")
            .IsRequired();

        builder
            .HasOne(p => p.ParentReservePayment)
            .WithMany(p => p.ChildPayments)
            .HasForeignKey(p => p.ParentReservePaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(p => p.Customer)
            .WithMany()
            .HasForeignKey(p => p.CustomerId);

        builder
            .HasOne(p => p.Reserve)
            .WithMany()
            .HasForeignKey(p => p.ReserveId);
    }
}
