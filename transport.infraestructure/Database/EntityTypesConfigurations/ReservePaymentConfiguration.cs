using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Reserves;
using Transport.Domain.Customers;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

internal class ReservePaymentConfiguration : IEntityTypeConfiguration<ReservePayment>
{
    public void Configure(EntityTypeBuilder<ReservePayment> builder)
    {
        builder.ToTable(nameof(ReservePayment));

        builder.HasKey(p => p.ReservePaymentId);

        builder.Property(p => p.Amount)
               .HasColumnType("decimal(18,2)")
               .IsRequired();

        builder.Property(p => p.Method)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        builder.Property(p => p.Status)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        builder.Property(p => p.StatusDetail)
               .HasColumnType("VARCHAR(MAX)")
               .IsRequired(false);

        // ID de pago externo (MercadoPago u otro) - puede ser null mientras esté pendiente
        builder.Property(p => p.PaymentExternalId)
               .HasColumnType("BIGINT")
               .IsRequired(false);

        // Datos del pagador (cuando no necesariamente es un Customer)
        builder.Property(p => p.PayerDocumentNumber)
               .HasColumnType("VARCHAR(50)")
               .IsRequired(false);

        builder.Property(p => p.PayerName)
               .HasColumnType("VARCHAR(150)")
               .IsRequired(false);

        builder.Property(p => p.PayerEmail)
               .HasColumnType("VARCHAR(150)")
               .IsRequired(false);

        // Respuesta cruda del PSP
        builder.Property(p => p.ResultApiExternalRawJson)
               .HasColumnType("NVARCHAR(MAX)")
               .IsRequired(false);

        // Relaciones
        builder.HasOne(p => p.Reserve)
               .WithMany()
               .HasForeignKey(p => p.ReserveId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Customer)
               .WithMany()
               .HasForeignKey(p => p.CustomerId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.ParentReservePayment)
               .WithMany(p => p.ChildPayments)
               .HasForeignKey(p => p.ParentReservePaymentId)
               .OnDelete(DeleteBehavior.Restrict);

        // Índices útiles
        builder.HasIndex(p => p.ReserveId);
        builder.HasIndex(p => p.CustomerId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.ParentReservePaymentId);
        builder.HasIndex(p => p.PaymentExternalId);
    }
}
