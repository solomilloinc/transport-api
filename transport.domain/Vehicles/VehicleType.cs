using Transport.SharedKernel;

namespace Transport.Domain.Vehicles;

public class VehicleType: IAuditable
{
    public int VehicleTypeId { get; set; }
    public string Name { get; set; } = null!;
    public int Quantity { get; set; }
    public string? ImageBase64 { get; set; }
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
