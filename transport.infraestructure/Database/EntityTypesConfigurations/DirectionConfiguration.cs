using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class DirectionConfiguration : IEntityTypeConfiguration<Direction>
{
    public void Configure(EntityTypeBuilder<Direction> builder)
    {
        builder.ToTable("Direction");
        builder.HasKey(d => d.DirectionId);
        builder.Property(d => d.Name).HasMaxLength(250).IsRequired();

        builder.HasOne(d => d.City)
               .WithMany(c => c.Directions)
               .HasForeignKey(d => d.CityId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
