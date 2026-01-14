using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using transport_api.Functions.Subscriptions.Base;
using Transport.Business.Tasks;
using Transport.Domain.Reserves;

namespace transport_api.Functions.Subscriptions;

public class CustomerReserveCreatedSubscriptionFunction : ServiceBusSubscriptionBase<CustomerReserveCreatedEvent>
{
    private readonly ISendReservationEmailTask _sendReservationEmailTask;

    public CustomerReserveCreatedSubscriptionFunction(
        ILogger<CustomerReserveCreatedSubscriptionFunction> logger,
        ISendReservationEmailTask sendReservationEmailTask)
        : base(logger)
    {
        _sendReservationEmailTask = sendReservationEmailTask;
    }

    [Function(nameof(CustomerReserveCreatedSubscriptionFunction))]
    public async Task Run(
        [ServiceBusTrigger("customerreservecreatedevent", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        await ProcessMessageAsync(message, messageActions);
    }

    protected override async Task HandleAsync(CustomerReserveCreatedEvent @event)
    {
        await _sendReservationEmailTask.ExecuteAsync(@event);
    }

    protected override string GetEventIdentifier(CustomerReserveCreatedEvent @event)
    {
        return $"Reserve:{@event.ReserveId}";
    }
}
