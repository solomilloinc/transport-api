namespace Transport.Domain;

public class Vehicle
{
    public int VehicleId { get; set; }
    public int VehicleTypeId { get; set; }
    public string InternalNumber { get; set; } = null!;

    public VehicleType VehicleType { get; set; } = null!;
}
