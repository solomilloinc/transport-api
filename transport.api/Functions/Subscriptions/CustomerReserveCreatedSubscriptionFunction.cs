using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Transport.Business.Tasks;
using Transport.Domain.Reserves;

namespace transport_api.Functions.Subscriptions;

public class CustomerReserveCreatedSubscriptionFunction
{
    private readonly ILogger<CustomerReserveCreatedSubscriptionFunction> _logger;
    private readonly ISendReservationEmailTask _sendReservationEmailTask;

    public CustomerReserveCreatedSubscriptionFunction(ILogger<CustomerReserveCreatedSubscriptionFunction> logger,
        ISendReservationEmailTask sendReservationEmailTask)
    {
        _logger = logger;
        _sendReservationEmailTask = sendReservationEmailTask;
    }

    [Function(nameof(CustomerReserveCreatedSubscriptionFunction))]
    public async Task Run(
        [ServiceBusTrigger("customerreservecreatedevent", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);
        _logger.LogInformation("Message Body: {body}", message.Body);
        _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);


        try
        {
            var json = message.Body.ToString();
            var @event = System.Text.Json.JsonSerializer.Deserialize<CustomerReserveCreatedEvent>(json);

            if (@event is null)
            {
                _logger.LogError("Message could not be deserialized.");
                // Reemplaza la línea problemática por la siguiente:
                await messageActions.DeadLetterMessageAsync(message, null, "InvalidPayload", "Failed to deserialize payload");
                return;
            }

            await _sendReservationEmailTask.ExecuteAsync(@event);
            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message.");
            await messageActions.DeadLetterMessageAsync(message, null, "ProcessingError", ex.Message);
        }
    }
}