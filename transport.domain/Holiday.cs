namespace transport.domain;
public class Holiday
{
    public int HolidayId { get; set; }
    public DateTime HolidayDate { get; set; }
    public string Description { get; set; } = null!;
}
