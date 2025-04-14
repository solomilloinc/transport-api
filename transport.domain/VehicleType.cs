namespace Transport.Domain;

public class VehicleType
{
    public int VehicleTypeId { get; set; }
    public string Name { get; set; } = null!;
    public int Quantity { get; set; }
    public string? ImageBase64 { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
