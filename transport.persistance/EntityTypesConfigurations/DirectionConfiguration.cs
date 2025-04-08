using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace transport.persistance.EntityTypesConfigurations;

public class DirectionConfiguration : IEntityTypeConfiguration<Direction>
{
    public void Configure(EntityTypeBuilder<Direction> builder)
    {
        builder.ToTable("Directions");
        builder.HasKey(d => d.DirectionId);
        builder.Property(d => d.Name).HasMaxLength(250).IsRequired();
        builder.HasOne(d => d.City)
               .WithMany()
               .HasForeignKey(d => d.CityId)
               .IsRequired();
    }
}
