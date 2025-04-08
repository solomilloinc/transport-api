namespace transport.domain;

public class City
{
    public int CityId { get; set; }
    public string Name { get; set; } = null!;

    public ICollection<Direction> Directions { get; set; } = new List<Direction>();
    public ICollection<Service> OriginServices { get; set; } = new List<Service>();
    public ICollection<Service> DestinationServices { get; set; } = new List<Service>();
}
