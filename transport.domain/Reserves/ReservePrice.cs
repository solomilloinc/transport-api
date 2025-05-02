using Transport.Domain.Services;

namespace Transport.Domain.Reserves;

public class ReservePrice
{
    public int ReservePriceId { get; set; }
    public int ServiceId { get; set; }
    public decimal Price { get; set; }
    public ReserveTypeIdEnum ReserveTypeId { get; set; }

    public Service Service { get; set; } = null!;
}
