using Transport.Domain.Directions;
using Transport.SharedKernel;

namespace Transport.Domain.Cities;

public class City : Entity, IAuditable
{
    public int CityId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;
    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public ICollection<Direction> Directions { get; set; } = new List<Direction>();
}

