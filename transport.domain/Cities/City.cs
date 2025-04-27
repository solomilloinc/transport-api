using Transport.SharedKernel;

namespace Transport.Domain.Cities;

public class City
{
    public int CityId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;

    public ICollection<Direction> Directions { get; set; } = new List<Direction>();
    public ICollection<Service> OriginServices { get; set; } = new List<Service>();
    public ICollection<Service> DestinationServices { get; set; } = new List<Service>();
}
