using System.Text.Json;
using transport.common;

public static class DomainEventExtensions
{
    public static OutboxMessage ToOutboxMessage(this IDomainEvent domainEvent, string? topic = null)
    {
        return new OutboxMessage
        {
            Id = domainEvent.EventId,
            OccurredOn = domainEvent.OccurredOn,
            Type = domainEvent.EventType,
            Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            Topic = topic
        };
    }
}
