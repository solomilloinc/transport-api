namespace Transport.SharedKernel.Contracts.FrequentSubscription;

public record FrequentSubscriptionCreateRequestDto(
    int CustomerId,
    int ReserveTypeId,
    int OutboundServiceId,
    int? InboundServiceId,
    int OutboundPickupLocationId,
    int OutboundDropoffLocationId,
    int? InboundPickupLocationId,
    int? InboundDropoffLocationId,
    DateTime? StartDate,
    DateTime? EndDate); 
