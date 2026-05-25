namespace Transport.SharedKernel.Contracts.FrequentSubscription;

/// <summary>
/// Vista previa, read-only e idempotente, de lo que haría cancelar una <c>FrequentSubscription</c>.
/// El frontend la usa para mostrar números concretos en el modal de confirmación.
/// </summary>
public record FrequentSubscriptionCancelPreviewDto(
    int FrequentSubscriptionId,
    int PassengersToCancel,
    decimal TotalRefundAmount);
