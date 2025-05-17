using Transport.Domain.Services;
using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

public class ReservePrice: IAuditable
{
    public int ReservePriceId { get; set; }
    public int ServiceId { get; set; }
    public decimal Price { get; set; }
    public ReserveTypeIdEnum ReserveTypeId { get; set; }
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;

    public string CreatedBy { get; set; } = null!;
    public string UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Service Service { get; set; } = null!;
}
