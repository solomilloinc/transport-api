using Transport.Domain.Reserves;
using Transport.SharedKernel;

namespace Transport.Domain.Drivers;

public class Driver: Entity
{
    public int DriverId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;
    public ICollection<Reserve> Reserves { get; set; } = new List<Reserve>();
}
