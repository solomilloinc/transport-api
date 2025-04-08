using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace transport.infraestructure.Database.EntityTypesConfigurations;

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("Holidays");
        builder.HasKey(h => h.HolidayId);
        builder.Property(h => h.HolidayDate).IsRequired();
        builder.HasIndex(h => h.HolidayDate).IsUnique();
        builder.Property(h => h.Description).HasMaxLength(255).IsRequired();
    }
}
