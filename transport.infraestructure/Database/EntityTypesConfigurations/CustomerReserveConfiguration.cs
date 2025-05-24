using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Customers;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class CustomerReserveConfiguration : IEntityTypeConfiguration<CustomerReserve>
{
    public void Configure(EntityTypeBuilder<CustomerReserve> builder)
    {
        builder.ToTable("CustomerReserve");

        builder.HasKey(cr => cr.CustomerReserveId);

        builder.Property(cr => cr.IsPayment).IsRequired();
        builder.Property(cr => cr.StatusPayment).HasMaxLength(50).IsRequired();
        builder.Property(cr => cr.Price).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(cr => cr.HasTraveled).HasDefaultValue(false);

        builder.HasOne(cr => cr.Reserve)
               .WithMany(r => r.CustomerReserves)
               .HasForeignKey(cr => cr.ReserveId);

        builder.HasOne(cr => cr.Customer)
               .WithMany(c => c.CustomerReserves)
               .HasForeignKey(cr => cr.CustomerId);

        builder.HasOne(cr => cr.PickupLocation)
               .WithMany(d => d.PickupCustomerReserves)
               .HasForeignKey(cr => cr.PickupLocationId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(cr => cr.DropoffLocation)
               .WithMany(d => d.DropoffCustomerReserves)
               .HasForeignKey(cr => cr.DropoffLocationId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.Property(r => r.PaymentMethod)
               .HasConversion<string>()
               .IsRequired();
    }
}
