using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

public record CustomerReserveCreatedEvent(
    int ReserveId,
    int TenantId,
    int? CustomerId,
    string CustomerEmail,
    string CustomerFullName,
    string ServiceName,
    string OriginName,
    string DestinationName,
    DateTime ReserveDate,
    TimeSpan DepartureHour,
    decimal TotalPrice
) : IDomainEvent
{
    public Guid EventId => Guid.NewGuid();

    public DateTime OccurredOn => DateTime.UtcNow;

    public string EventType => nameof(CustomerReserveCreatedEvent);
}
