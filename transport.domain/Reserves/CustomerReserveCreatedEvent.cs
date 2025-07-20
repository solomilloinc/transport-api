using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

public record CustomerReserveCreatedEvent(int ReserveId, int CustomerId) : IDomainEvent
{
    public Guid EventId => Guid.NewGuid();

    public DateTime OccurredOn => DateTime.UtcNow;

    public string EventType => nameof(CustomerReserveCreatedEvent);
}
