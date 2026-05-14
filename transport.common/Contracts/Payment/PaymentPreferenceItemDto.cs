namespace Transport.SharedKernel.Contracts.Payment;

public record PaymentPreferenceItemDto(
    string Title,
    decimal UnitPrice,
    string Description);
