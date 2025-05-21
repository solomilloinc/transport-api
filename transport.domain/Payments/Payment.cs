using Transport.SharedKernel;

namespace Transport.Domain.Payments;

public class Payment: Entity, IAuditable
{
    public int PaymentId { get; set; }
    public long? PaymentMpId { get; set; }
    public string Email { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public DateTime? DateApproved { get; set; }
    public string RawJson { get; set; } = null!;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
}
