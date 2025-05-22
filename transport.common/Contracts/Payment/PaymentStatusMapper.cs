namespace Transport.SharedKernel.Contracts.Payment;

public static class PaymentStatusMapper
{
    public static MpPaymentStatusEnum ToPaymentStatus(this string status)
    {
        return status?.ToLower() switch
        {
            "pending" => MpPaymentStatusEnum.Pending,
            "approved" => MpPaymentStatusEnum.Approved,
            "authorized" => MpPaymentStatusEnum.Authorized,
            "in_process" => MpPaymentStatusEnum.InProcess,
            "in_mediation" => MpPaymentStatusEnum.InMediation,
            "rejected" => MpPaymentStatusEnum.Rejected,
            "cancelled" => MpPaymentStatusEnum.Cancelled,
            "refunded" => MpPaymentStatusEnum.Refunded,
            "charged_back" => MpPaymentStatusEnum.ChargedBack,
            _ => MpPaymentStatusEnum.None
        };
    }

    public static PaymentStatusDetail ToPaymentStatusDetail(this string statusDetail)
    {
        return statusDetail?.ToLower() switch
        {
            "accredited" => PaymentStatusDetail.Accredited,
            "pending_contingency" => PaymentStatusDetail.PendingContingency,
            "pending_review_manual" => PaymentStatusDetail.PendingReviewManual,
            "cc_rejected_bad_filled_card_number" => PaymentStatusDetail.CcRejectedBadFilledCardNumber,
            "cc_rejected_bad_filled_date" => PaymentStatusDetail.CcRejectedBadFilledDate,
            "cc_rejected_insufficient_amount" => PaymentStatusDetail.CcRejectedInsufficientAmount,
            "cc_rejected_blacklist" => PaymentStatusDetail.CcRejectedBlacklist,
            "cc_rejected_high_risk" => PaymentStatusDetail.CcRejectedHighRisk,
            "cc_rejected_call_for_authorize" => PaymentStatusDetail.CcRejectedCallForAuthorize,
            _ => PaymentStatusDetail.Other
        };
    }
}
