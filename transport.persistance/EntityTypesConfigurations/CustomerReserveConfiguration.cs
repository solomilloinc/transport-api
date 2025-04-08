using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace transport.persistance.EntityTypesConfigurations;

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
        builder.HasOne(cr => cr.Customer).WithMany().HasForeignKey(cr => cr.CustomerId);
        builder.HasOne(cr => cr.Reserve).WithMany().HasForeignKey(cr => cr.ReserveId);
        builder.HasOne(cr => cr.PickupLocation).WithMany().HasForeignKey(cr => cr.PickupLocationId);
        builder.HasOne(cr => cr.DropoffLocation).WithMany().HasForeignKey(cr => cr.DropoffLocationId);
    }
}
