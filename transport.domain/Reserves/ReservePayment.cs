using Transport.Domain.Customers;
using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

public class ReservePayment : Entity, IAuditable
{
    public int ReservePaymentId { get; set; }
    public int ReserveId { get; set; }
    public PaymentMethodEnum Method { get; set; }
    public StatusPaymentEnum Status { get; set; }
    public string StatusDetail { get; set; }
    public string ResultApiExternalRawJson { get; set; }
    public int? CustomerId { get; set; }
    public decimal Amount { get; set; }
    public long? PaymentExternalId { get; set; }
    public Reserve Reserve { get; set; } = null!;
    public Customer? Customer { get; set; } = null!;

    public string? PayerName { get; set; }
    public string? PayerDocumentNumber { get; set; }
    public string? PayerEmail { get; set; }

    public int? ParentReservePaymentId { get; set; }
    public ReservePayment? ParentReservePayment { get; set; }
    public ICollection<ReservePayment> ChildPayments { get; set; } = new List<ReservePayment>();

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
}
