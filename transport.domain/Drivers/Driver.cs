using transport.common;

namespace transport.domain.Drivers;

public class Driver: Entity
{
    public int DriverId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;

    public ICollection<Reserve> Reserves { get; set; } = new List<Reserve>();
}
