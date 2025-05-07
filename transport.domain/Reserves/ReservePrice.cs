using Transport.Domain.Services;
using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

public class ReservePrice
{
    public int ReservePriceId { get; set; }
    public int ServiceId { get; set; }
    public decimal Price { get; set; }
    public ReserveTypeIdEnum ReserveTypeId { get; set; }
    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;

    public Service Service { get; set; } = null!;
}
