using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace transport.persistance.EntityTypesConfigurations;

public class ServiceCustomerConfiguration : IEntityTypeConfiguration<ServiceCustomer>
{
    public void Configure(EntityTypeBuilder<ServiceCustomer> builder)
    {
        builder.ToTable("Services_Customers");
        builder.HasKey(sc => new { sc.ServiceId, sc.CustomerId });
        builder.HasOne(sc => sc.Service).WithMany().HasForeignKey(sc => sc.ServiceId);
        builder.HasOne(sc => sc.Customer).WithMany().HasForeignKey(sc => sc.CustomerId);
    }
}
