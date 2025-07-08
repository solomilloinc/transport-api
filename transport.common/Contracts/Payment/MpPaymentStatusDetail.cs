namespace Transport.SharedKernel.Contracts.Payment;

public enum MpPaymentStatusDetail
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