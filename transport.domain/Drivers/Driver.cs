using Transport.Domain.Reserves;
using Transport.SharedKernel;

namespace Transport.Domain.Drivers;

public class Driver: Entity, IAuditable
{
    public int DriverId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;
    public ICollection<Reserve> Reserves { get; set; } = new List<Reserve>();

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
}
