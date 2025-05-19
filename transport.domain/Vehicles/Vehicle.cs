using Transport.SharedKernel;

namespace Transport.Domain.Vehicles;

public class Vehicle: IAuditable
{
    public int VehicleId { get; set; }
    public int VehicleTypeId { get; set; }
    public string InternalNumber { get; set; } = null!;
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;
    public int AvailableQuantity { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public VehicleType VehicleType { get; set; } = null!;
}
