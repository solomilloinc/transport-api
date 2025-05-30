using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Services;

namespace Transport.Infraestructure.Database.EntityTypesConfigurations;
public class ServiceScheduleConfiguration : IEntityTypeConfiguration<ServiceSchedule>
{
    public void Configure(EntityTypeBuilder<ServiceSchedule> builder)
    {
        builder.ToTable("ServiceSchedule");

        builder.HasKey(s => s.ServiceScheduleId);

        builder.Property(s => s.StartDay).IsRequired();
        builder.Property(s => s.EndDay).IsRequired();
        builder.Property(s => s.DepartureHour).IsRequired();
        builder.Property(s => s.IsHoliday).IsRequired();
        builder.Property(s => s.Status).IsRequired();

        builder.HasOne(s => s.Service)
               .WithMany(service => service.Schedules)
               .HasForeignKey(s => s.ServiceId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Restrict); 
    }
}
