namespace Transport.SharedKernel.Contracts.FrequentSubscription;

public record FrequentSubscriptionUpdateRequestDto(
    int OutboundPickupLocationId,
    int OutboundDropoffLocationId,
    int? InboundPickupLocationId,
    int? InboundDropoffLocationId,
    DateTime? StartDate,
    DateTime? EndDate);
