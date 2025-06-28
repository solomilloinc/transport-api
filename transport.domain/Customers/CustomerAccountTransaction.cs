using Transport.Domain.Reserves;
using Transport.SharedKernel;

namespace Transport.Domain.Customers;

public class CustomerAccountTransaction : Entity, IAuditable
{
    public int CustomerAccountTransactionId { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    // Puede ser "Charge", "Payment", "Adjustment", etc.
    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public string? Description { get; set; }

    public int? RelatedReserveId { get; set; }
    public Reserve? RelatedReserve { get; set; }
    public int? ReservePaymentId { get; set; }
    public ReservePayment? ReservePayment { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate
    {
        get; set;
    }
}

public enum TransactionType
{
    Charge = 1,
    Payment = 2,
    Adjustment = 3,
    Refund = 4
}
