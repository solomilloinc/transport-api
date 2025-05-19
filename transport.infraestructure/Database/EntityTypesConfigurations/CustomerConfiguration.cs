using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Customers;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customer");
        builder.HasKey(c => c.CustomerId);
        builder.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(150).IsRequired();
        builder.HasIndex(c => c.Email).IsUnique();
        builder.Property(c => c.DocumentNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.DocumentNumber).IsUnique();
        builder.Property(c => c.Phone1).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Phone2).HasMaxLength(20);
        builder.Property(r => r.Status).IsRequired();
    }
}
