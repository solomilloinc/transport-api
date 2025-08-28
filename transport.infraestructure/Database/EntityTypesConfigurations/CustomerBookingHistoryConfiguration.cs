using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Customers;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class CustomerBookingHistoryConfiguration : IEntityTypeConfiguration<CustomerBookingHistory>
{
    public void Configure(EntityTypeBuilder<CustomerBookingHistory> builder)
    {
        builder.ToTable("CustomerBookingHistory");

        builder.HasKey(c => c.CustomerBookingHistoryId);

        builder.Property(c => c.Role)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        builder.Property(c => c.BookingDate)
               .IsRequired();

        builder.HasOne(c => c.Customer)
               .WithMany(cu => cu.BookingHistories)
               .HasForeignKey(c => c.CustomerId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(c => c.Reserve)
               .WithMany()
               .HasForeignKey(c => c.ReserveId)
               .OnDelete(DeleteBehavior.NoAction);

        // Un cliente no debería tener dos entradas iguales para la misma reserva y rol
        builder.HasIndex(c => new { c.CustomerId, c.ReserveId, c.Role })
               .IsUnique();

        builder.HasIndex(c => c.BookingDate);
    }
}
