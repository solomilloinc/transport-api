namespace Transport.Business.Messaging;

public interface IOutboxDispatcher
{
    Task DispatchUnprocessedMessagesAsync(CancellationToken cancellationToken = default);
}
