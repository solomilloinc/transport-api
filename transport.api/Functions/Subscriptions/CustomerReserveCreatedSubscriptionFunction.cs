using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using transport_api.Functions.Subscriptions.Base;
using Transport.Business.Authentication;
using Transport.Business.Tasks;
using Transport.Domain.Reserves;
using Transport.Infraestructure.Authentication;

namespace transport_api.Functions.Subscriptions;

public class CustomerReserveCreatedSubscriptionFunction : ServiceBusSubscriptionBase<CustomerReserveCreatedEvent>
{
    private readonly ISendReservationEmailTask _sendReservationEmailTask;
    private readonly ITenantContext _tenantContext;

    public CustomerReserveCreatedSubscriptionFunction(
        ILogger<CustomerReserveCreatedSubscriptionFunction> logger,
        ISendReservationEmailTask sendReservationEmailTask,
        ITenantContext tenantContext)
        : base(logger)
    {
        _sendReservationEmailTask = sendReservationEmailTask;
        _tenantContext = tenantContext;
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
        // Set tenant context from event payload
        if (_tenantContext is TenantContext tc)
        {
            tc.TenantId = @event.TenantId;
        }

        await _sendReservationEmailTask.ExecuteAsync(@event);
    }

    protected override string GetEventIdentifier(CustomerReserveCreatedEvent @event)
    {
        return $"Reserve:{@event.ReserveId}";
    }
}
