using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace transport.infraestructure.Database.EntityTypesConfigurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");
        builder.HasKey(r => r.RoleId);
        builder.Property(r => r.Name).HasMaxLength(250).IsRequired();
    }
}
