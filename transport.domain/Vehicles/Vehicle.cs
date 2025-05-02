using Transport.SharedKernel;

namespace Transport.Domain.Vehicles;

public class Vehicle
{
    public int VehicleId { get; set; }
    public int VehicleTypeId { get; set; }
    public string InternalNumber { get; set; } = null!;
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;
    public int AvailableQuantity { get; set; }

    public VehicleType VehicleType { get; set; } = null!;
}
