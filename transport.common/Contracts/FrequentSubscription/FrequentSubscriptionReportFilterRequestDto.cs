namespace Transport.SharedKernel.Contracts.FrequentSubscription;

public record FrequentSubscriptionReportFilterRequestDto(
    int? CustomerId,
    int? OutboundServiceId,
    int? InboundServiceId,
    int? ReserveTypeId,
    EntityStatusEnum? Status,
    DateTime? ActiveAtDate);
