namespace Transport.SharedKernel.Contracts.Reserve;

/// <summary>
/// Reason a price was adjusted away from the originally requested type.
/// </summary>
public enum QuoteReasonEnum
{
    /// <summary>
    /// The IdaVuelta combo discount was lost because outbound and return
    /// reservations are on different calendar days.
    /// </summary>
    RoundTripDifferentDay = 1
}

public record ReserveQuoteRequestItemDto(
    int TripId,
    DateTime ReserveDate,
    int ReserveTypeId,
    int? DropoffLocationId,
    int PassengerCount);

public record ReserveQuoteRequestDto(List<ReserveQuoteRequestItemDto> Items);

public record ReserveQuoteResponseItemDto(
    int TripId,
    int RequestedReserveTypeId,
    int AppliedReserveTypeId,
    decimal UnitPrice,
    decimal Subtotal,
    QuoteReasonEnum? Reason);

public record DiscountLostDto(string Code, string Message);

public record ReserveQuoteResponseDto(
    List<ReserveQuoteResponseItemDto> Items,
    decimal Total,
    List<DiscountLostDto> DiscountsLost);
