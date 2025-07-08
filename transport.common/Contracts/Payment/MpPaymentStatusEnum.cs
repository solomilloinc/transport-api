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
