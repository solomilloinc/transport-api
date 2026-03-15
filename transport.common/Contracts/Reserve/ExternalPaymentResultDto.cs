namespace Transport.SharedKernel.Contracts.Reserve;

public record ExternalPaymentResultDto(
    long? PaymentExternalId,
    string ExternalReference,
    string Status,
    string? StatusDetail,
    string RawJson
);
