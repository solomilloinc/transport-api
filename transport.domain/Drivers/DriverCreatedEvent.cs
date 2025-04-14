using Transport.SharedKernel;

namespace Transport.Domain.Drivers;

public record DriverCreatedEvent(int DriverId) : IDomainEvent
{
    public Guid EventId => Guid.NewGuid();

    public DateTime OccurredOn => DateTime.UtcNow;

    public string EventType => nameof(DriverCreatedEvent);
}
