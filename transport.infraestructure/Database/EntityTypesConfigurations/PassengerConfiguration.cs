using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transport.Domain.Passengers;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;

public class PassengerConfiguration : IEntityTypeConfiguration<Passenger>
{
    public void Configure(EntityTypeBuilder<Passenger> builder)
    {
        builder.ToTable("Passenger");

        builder.HasKey(p => p.PassengerId);

        builder.Property(p => p.FirstName)
               .HasColumnType("VARCHAR(100)")
               .IsRequired();

        builder.Property(p => p.LastName)
               .HasColumnType("VARCHAR(100)")
               .IsRequired();

        builder.Property(p => p.DocumentNumber)
               .HasColumnType("VARCHAR(50)")
               .IsRequired();

        builder.Property(p => p.Email)
               .HasColumnType("VARCHAR(150)")
               .IsRequired(false);

        builder.Property(p => p.Phone)
               .HasColumnType("VARCHAR(30)")
               .IsRequired(false);

        builder.Property(p => p.Price)
               .HasColumnType("decimal(18,2)")
               .IsRequired();

        builder.Property(p => p.Status)
               .HasConversion<string>()
               .HasColumnType("VARCHAR(20)")
               .IsRequired();

        builder.Property(p => p.PickupAddress)
               .HasColumnType("VARCHAR(250)")
               .IsRequired(false);

        builder.Property(p => p.DropoffAddress)
               .HasColumnType("VARCHAR(250)")
               .IsRequired(false);

        builder.Property(p => p.HasTraveled)
               .HasDefaultValue(false);

        // Relaciones
        builder.HasOne(p => p.Reserve)
               .WithMany(r => r.Passengers)
               .HasForeignKey(p => p.ReserveId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.Customer)
               .WithMany(c => c.Passengers)
               .HasForeignKey(p => p.CustomerId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.PickupLocation)
               .WithMany()
               .HasForeignKey(p => p.PickupLocationId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.DropoffLocation)
               .WithMany()
               .HasForeignKey(p => p.DropoffLocationId)
               .OnDelete(DeleteBehavior.NoAction);

        // Auditoría
        builder.Property(p => p.CreatedBy)
               .HasColumnType("VARCHAR(100)")
               .IsRequired();

        builder.Property(p => p.UpdatedBy)
               .HasColumnType("VARCHAR(100)")
               .IsRequired(false);

        // Índices
        builder.HasIndex(p => p.ReserveId);
        builder.HasIndex(p => p.CustomerId);
        builder.HasIndex(p => p.Status);

        // Evita duplicar el mismo documento en la misma reserva (regla de negocio)
        builder.HasIndex(p => new { p.ReserveId, p.DocumentNumber })
               .IsUnique();
    }
}
