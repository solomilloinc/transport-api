using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Users;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");
        builder.HasKey(r => r.RoleId);
        builder.Property(r => r.Name).HasMaxLength(250).IsRequired();

        builder.HasData(
        new Role { RoleId = (int)RoleEnum.Admin, Name = "Administrador" },
        new Role { RoleId = (int)RoleEnum.User, Name = "Cliente" }
        );
    }
}
