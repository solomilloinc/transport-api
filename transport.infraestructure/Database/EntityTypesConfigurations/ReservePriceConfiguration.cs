using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Reserves;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class ReservePriceConfiguration : IEntityTypeConfiguration<ReservePrice>
{
    public void Configure(EntityTypeBuilder<ReservePrice> builder)
    {
        builder.ToTable("ReservePrice");
        builder.HasKey(rp => rp.ReservePriceId);
        builder.Property(rp => rp.Price).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(rp => rp.ReserveTypeId).HasMaxLength(50).IsRequired();

        builder.HasIndex(rp => rp.ServiceId).IsUnique();

        builder.HasOne(rp => rp.Service)
       .WithMany(s => s.ReservePrices)
       .HasForeignKey(rp => rp.ServiceId);
    }
}
