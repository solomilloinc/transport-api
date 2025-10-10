using Transport.SharedKernel;

namespace Transport.Domain.Services;

public class ServiceSchedule : Entity, IAuditable
{
    public int ServiceScheduleId { get; set; }
    public int ServiceId { get; set; }
    public TimeSpan DepartureHour { get; set; }
    public bool IsHoliday { get; set; }
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;
    public Service Service { get; set; } = null!;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
}
