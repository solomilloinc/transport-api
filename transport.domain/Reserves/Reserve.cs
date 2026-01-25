using Transport.Domain.Cities;
using Transport.Domain.Customers;
using Transport.Domain.Drivers;
using Transport.Domain.Passengers;
using Transport.Domain.Services;
using Transport.Domain.Trips;
using Transport.Domain.Users;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;

namespace Transport.Domain.Reserves;
public class Reserve: Entity, IAuditable
{
    public int ReserveId { get; set; }
    public DateTime ReserveDate { get; set; }
    public int VehicleId { get; set; }
    public int? DriverId { get; set; }
    public int? ServiceId { get; set;}
    public int? ServiceScheduleId { get; set; }
    public int TripId { get; set; }
    public ReserveStatusEnum Status { get; set; }
    public string ServiceName { get; set; }
    public string OriginName { get; set; }
    public string DestinationName { get; set; }
    public TimeSpan DepartureHour { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public bool IsHoliday { get; set; }

    // Optimistic Concurrency Control
    public byte[] RowVersion { get; set; } = null!;

    public Vehicle Vehicle { get; set; } = null!;
    public Driver? Driver { get; set; }
    public Service? Service { get; set; }
    public ServiceSchedule? ServiceSchedule { get; set; }
    public Trip Trip { get; set; } = null!;
    public ICollection<Passenger> Passengers { get; set; } = new List<Passenger>();
    public ICollection<ReserveDirection> AllowedDirections { get; set; } = new List<ReserveDirection>();

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
}