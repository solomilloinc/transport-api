using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using transport.domain;

namespace transport.infraestructure.Database.EntityTypesConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.UserId);
        builder.HasIndex(u => u.CustomerId).IsUnique();
        builder.HasOne(u => u.Customer)
               .WithOne()
               .HasForeignKey<User>(u => u.CustomerId);
        builder.HasOne(u => u.Role)
               .WithMany()
               .HasForeignKey(u => u.RoleId)
               .IsRequired();
    }
}
