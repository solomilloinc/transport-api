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

        builder.Property(p => p.ExternalReference)
            .HasMaxLength(100);

        builder.Property(p => p.Email)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.StatusDetail)
           .HasConversion<string>()
           .HasMaxLength(50)
           .IsRequired();

        builder.Property(p => p.PaymentTypeId)
            .HasMaxLength(50);

        builder.Property(p => p.PaymentMethodId)
            .HasMaxLength(50);

        builder.Property(p => p.Installments);

        builder.Property(p => p.CardLastFourDigits)
            .HasMaxLength(4);

        builder.Property(p => p.CardHolderName)
            .HasMaxLength(100);

        builder.Property(p => p.AuthorizationCode)
            .HasMaxLength(50);

        builder.Property(p => p.FeeAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.NetReceivedAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.RefundedAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.Captured);

        builder.Property(p => p.DateCreatedMp)
            .HasColumnType("datetime");

        builder.Property(p => p.DateApproved)
            .HasColumnType("datetime");

        builder.Property(p => p.DateLastUpdated)
            .HasColumnType("datetime");

        builder.Property(p => p.RawJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(p => p.TransactionDetails)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(p => p.PaymentMpId);
        builder.HasIndex(p => p.ExternalReference);
        builder.HasIndex(p => p.Email);
    }

}
