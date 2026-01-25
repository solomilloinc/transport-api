using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Reserves;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class ReserveDirectionConfiguration : IEntityTypeConfiguration<ReserveDirection>
{
    public void Configure(EntityTypeBuilder<ReserveDirection> builder)
    {
        builder.ToTable("ReserveDirection");

        builder.HasKey(rd => rd.ReserveDirectionId);

        builder.HasOne(rd => rd.Reserve)
               .WithMany(r => r.AllowedDirections)
               .HasForeignKey(rd => rd.ReserveId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rd => rd.Direction)
               .WithMany()
               .HasForeignKey(rd => rd.DirectionId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint: one direction per reserve
        builder.HasIndex(rd => new { rd.ReserveId, rd.DirectionId }).IsUnique();
    }
}
