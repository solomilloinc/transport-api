namespace transport.domain;

public class ReservePrice
{
    public int ReservePriceId { get; set; }
    public int ServiceId { get; set; }
    public decimal Price { get; set; }
    public string ReserveTypeId { get; set; } = null!;

    public Service Service { get; set; } = null!;
}
