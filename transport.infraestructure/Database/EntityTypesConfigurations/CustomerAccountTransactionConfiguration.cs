using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Customers;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class CustomerAccountTransactionConfiguration : IEntityTypeConfiguration<CustomerAccountTransaction>
{
    public void Configure(EntityTypeBuilder<CustomerAccountTransaction> builder)
    {
        builder.ToTable("CustomerAccountTransactions");

        builder.HasKey(t => t.CustomerAccountTransactionId);

        builder.Property(t => t.Date)
               .IsRequired();

        builder.Property(t => t.Type)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(t => t.Amount)
               .HasColumnType("decimal(18,2)")
               .IsRequired();

        builder.Property(t => t.Description)
               .HasMaxLength(250);

        builder.HasOne(t => t.Customer)
               .WithMany(c => c.AccountTransactions)
               .HasForeignKey(t => t.CustomerId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.RelatedReserve)
               .WithMany()
               .HasForeignKey(t => t.RelatedReserveId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ReservePayment)
            .WithMany()
            .HasForeignKey(x => x.ReservePaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.Date);
        builder.HasIndex(t => t.Type);
    }
}
