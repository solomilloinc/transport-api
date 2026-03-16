using Transport.SharedKernel;

namespace Transport.Domain;
public class Holiday : IAuditable, ITenantScoped
{
    public int HolidayId { get; set; }
    public int TenantId { get; set; }
    public DateTime HolidayDate { get; set; }
    public string Description { get; set; } = null!;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
}
