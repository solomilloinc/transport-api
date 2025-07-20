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

        builder.Property(cr => cr.ServiceName)
                .HasMaxLength(250)
                .HasColumnType("VARCHAR(250)")
                .IsRequired();

        builder.Property(cr => cr.OriginCityName)
            .HasMaxLength(100)
            .HasColumnType("VARCHAR(100)")
            .IsRequired();

        builder.Property(cr => cr.DestinationCityName)
            .HasMaxLength(100)
            .HasColumnType("VARCHAR(100)")
            .IsRequired();

        builder.Property(cr => cr.VehicleInternalNumber)
            .HasMaxLength(20)
            .HasColumnType("VARCHAR(20)")
            .IsRequired();

        builder.Property(cr => cr.DriverName)
            .HasMaxLength(100)
            .HasColumnType("VARCHAR(100)")
            .IsRequired(false);

        builder.Property(cr => cr.PickupAddress)
            .HasMaxLength(250)
            .HasColumnType("VARCHAR(250)")
            .IsRequired(false);

        builder.Property(cr => cr.DropoffAddress)
            .HasMaxLength(250)
            .HasColumnType("VARCHAR(250)")
            .IsRequired(false);

        builder.Property(cr => cr.CustomerFullName)
            .HasMaxLength(250)
            .HasColumnType("VARCHAR(250)")
            .IsRequired();

        builder.Property(cr => cr.DocumentNumber)
            .HasMaxLength(50)
            .HasColumnType("VARCHAR(50)")
            .IsRequired();

        builder.Property(cr => cr.CustomerEmail)
            .HasMaxLength(150)
            .HasColumnType("VARCHAR(150)")
            .IsRequired();

        builder.Property(cr => cr.Phone1)
            .HasMaxLength(30)
            .HasColumnType("VARCHAR(30)")
            .IsRequired(false);

        builder.Property(cr => cr.Phone2)
            .HasMaxLength(30)
            .HasColumnType("VARCHAR(30)")
            .IsRequired(false);

        builder.Property(r => r.Status)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        builder.Property(r => r.ReserveDate).IsRequired();
    }
}
