using Transport.Domain.Cities;
using Transport.Domain.Reserves;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;

namespace Transport.Domain.Services;
public class Service : Entity, IAuditable
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = null!;
    public int OriginId { get; set; }
    public int DestinationId { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public int VehicleId { get; set; }
    public DayOfWeek StartDay { get; set; }
    public DayOfWeek EndDay { get; set; }
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public City Origin { get; set; } = null!;
    public City Destination { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;

    public ICollection<ServiceCustomer> Customers { get; set; } = new List<ServiceCustomer>();
    public ICollection<Reserve> Reserves { get; set; } = new List<Reserve>();
    public ICollection<ReservePrice> ReservePrices { get; set; } = new List<ReservePrice>();
    public ICollection<ServiceSchedule> Schedules { get; set; } = new List<ServiceSchedule>();

    public bool IsDayWithinScheduleRange(DayOfWeek day)
    {
        if (StartDay == EndDay)
            return day == StartDay;

        if (StartDay < EndDay)
            return day >= StartDay && day <= EndDay;

        return day >= StartDay || day <= EndDay;
    }
}

