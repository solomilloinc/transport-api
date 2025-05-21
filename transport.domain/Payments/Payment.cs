using Transport.Domain.Customers;
using Transport.SharedKernel.Contracts.Payment;

namespace Transport.Domain.Payments;

public class Payment
{
    public int PaymentId { get; set; }
    public long? PaymentMpId { get; set; }
    public string Email { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public DateTime? DateApproved { get; set; }
    public string RawJson { get; set; } = null!;
}
