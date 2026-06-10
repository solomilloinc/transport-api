namespace Transport.SharedKernel.Contracts.Reserve;

public record ExternalPaymentResultDto(
    long? PaymentExternalId,
    string ExternalReference,
    string Status,
    string? StatusDetail,
    string RawJson,
    // Datos del Payer de MercadoPago para resolver el Customer pagador (ADR 0008).
    string? PayerDocumentNumber = null,
    string? PayerEmail = null,
    string? PayerFirstName = null,
    string? PayerLastName = null,
    string? CardholderName = null
);
