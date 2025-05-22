namespace Transport.SharedKernel.Contracts.Payment;

public enum MpPaymentStatusEnum
{
    Pending,
    Approved,
    Authorized,
    InProcess,
    InMediation,
    Rejected,
    Cancelled,
    Refunded,
    ChargedBack,
    None
}

public enum PaymentStatusDetail
{
    Accredited,
    PendingContingency,
    PendingReviewManual,
    CcRejectedBadFilledCardNumber,
    CcRejectedBadFilledDate,
    CcRejectedInsufficientAmount,
    CcRejectedBlacklist,
    CcRejectedHighRisk,
    CcRejectedCallForAuthorize,
    Other
}
