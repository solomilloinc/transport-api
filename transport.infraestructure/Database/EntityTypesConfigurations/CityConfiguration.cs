using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("City");
        builder.HasKey(c => c.CityId);
        builder.Property(c => c.Name).HasMaxLength(250).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
