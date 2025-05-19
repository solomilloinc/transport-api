using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Users;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");
        builder.HasKey(u => u.UserId);
        builder.HasIndex(u => u.CustomerId).IsUnique();
        builder.Property(u => u.Status).IsRequired();

        builder.HasOne(u => u.Customer)
               .WithOne(c => c.User)
               .HasForeignKey<User>(u => u.CustomerId)
               .IsRequired(false);

        builder.HasOne(u => u.Role)
               .WithMany(r => r.Users)
               .HasForeignKey(u => u.RoleId)
               .IsRequired();
    }
}
