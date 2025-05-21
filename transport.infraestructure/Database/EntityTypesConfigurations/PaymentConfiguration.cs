using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Payments;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payment");

        builder.HasKey(p => p.PaymentId);

        builder.Property(p => p.PaymentId)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.PaymentMpId)
            .HasColumnType("bigint");

        builder.Property(p => p.Email)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.DateApproved)
            .HasColumnType("datetime");

        builder.Property(p => p.RawJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
    }
}
