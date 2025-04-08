using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace transport.infraestructure.Database.EntityTypesConfigurations;

public class ReservePriceConfiguration : IEntityTypeConfiguration<ReservePrice>
{
    public void Configure(EntityTypeBuilder<ReservePrice> builder)
    {
        builder.ToTable("ReservePrice");
        builder.HasKey(rp => rp.ReservePriceId);
        builder.Property(rp => rp.Price).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(rp => rp.ReserveTypeId).HasMaxLength(50).IsRequired();
        builder.HasOne(rp => rp.Service).WithMany().HasForeignKey(rp => rp.ServiceId);
    }
}
