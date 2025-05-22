using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Payment;

namespace Transport.Domain.Payments;

public class Payment: Entity, IAuditable
{
    public int PaymentId { get; set; }
    public long? PaymentMpId { get; set; }
    public string? ExternalReference { get; set; }
    public string Email { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ARS";

    public MpPaymentStatusEnum Status { get; set; }
    public PaymentStatusDetail StatusDetail { get; set; }

    public string? PaymentTypeId { get; set; }
    public string? PaymentMethodId { get; set; }
    public int? Installments { get; set; }
    public string? CardLastFourDigits { get; set; }
    public string? CardHolderName { get; set; }
    public string? AuthorizationCode { get; set; }

    public decimal? FeeAmount { get; set; }
    public decimal? NetReceivedAmount { get; set; }
    public decimal? RefundedAmount { get; set; }
    public bool? Captured { get; set; }

    public DateTime? DateCreatedMp { get; set; }
    public DateTime? DateApproved { get; set; }
    public DateTime? DateLastUpdated { get; set; }

    public string RawJson { get; set; } = null!;
    public string? TransactionDetails { get; set; }

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
}
