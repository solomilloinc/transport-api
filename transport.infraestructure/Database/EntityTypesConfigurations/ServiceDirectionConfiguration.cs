using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Services;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class ServiceDirectionConfiguration : IEntityTypeConfiguration<ServiceDirection>
{
    public void Configure(EntityTypeBuilder<ServiceDirection> builder)
    {
        builder.ToTable("ServiceDirection");

        builder.HasKey(sd => sd.ServiceDirectionId);

        builder.HasOne(sd => sd.Service)
               .WithMany(s => s.AllowedDirections)
               .HasForeignKey(sd => sd.ServiceId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sd => sd.Direction)
               .WithMany()
               .HasForeignKey(sd => sd.DirectionId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint: one direction per service
        builder.HasIndex(sd => new { sd.ServiceId, sd.DirectionId }).IsUnique();
    }
}
