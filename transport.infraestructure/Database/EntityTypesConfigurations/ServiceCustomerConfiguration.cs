using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Services;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class ServiceCustomerConfiguration : IEntityTypeConfiguration<ServiceCustomer>
{
    public void Configure(EntityTypeBuilder<ServiceCustomer> builder)
    {
        builder.ToTable("ServiceCustomer");

        builder.HasKey(sc => sc.ServiceCustomerId);

        builder.HasOne(sc => sc.Service)
               .WithMany(s => s.Customers)
               .HasForeignKey(sc => sc.ServiceId)
               .IsRequired();

        builder.HasOne(sc => sc.Customer)
               .WithMany(c => c.Services)
               .HasForeignKey(sc => sc.CustomerId)
               .IsRequired();
    }
}
