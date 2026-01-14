using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace transport_api.Functions.Subscriptions.Base;

public abstract class ServiceBusSubscriptionBase<TEvent> where TEvent : class
{
    private const int DefaultMaxRetryAttempts = 3;

    protected readonly ILogger Logger;
    protected virtual int MaxRetryAttempts => DefaultMaxRetryAttempts;

    protected ServiceBusSubscriptionBase(ILogger logger)
    {
        Logger = logger;
    }

    protected async Task ProcessMessageAsync(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        var eventTypeName = typeof(TEvent).Name;

        Logger.LogInformation(
            "[{EventType}] Processing message {MessageId}, Attempt {DeliveryCount}/{MaxRetries}",
            eventTypeName, message.MessageId, message.DeliveryCount, MaxRetryAttempts);

        // 1. Deserializar el mensaje
        TEvent? @event;
        try
        {
            var json = message.Body.ToString();
            @event = JsonSerializer.Deserialize<TEvent>(json);
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex,
                "[{EventType}] Failed to deserialize message {MessageId}. Sending to dead-letter.",
                eventTypeName, message.MessageId);

            await messageActions.DeadLetterMessageAsync(
                message,
                deadLetterReason: "InvalidPayload",
                deadLetterErrorDescription: $"JSON deserialization failed: {ex.Message}");
            return;
        }

        if (@event is null)
        {
            Logger.LogError(
                "[{EventType}] Message {MessageId} deserialized to null. Sending to dead-letter.",
                eventTypeName, message.MessageId);

            await messageActions.DeadLetterMessageAsync(
                message,
                deadLetterReason: "NullPayload",
                deadLetterErrorDescription: "Event deserialized to null");
            return;
        }

        // 2. Procesar el mensaje
        try
        {
            await HandleAsync(@event);
            await messageActions.CompleteMessageAsync(message);

            Logger.LogInformation(
                "[{EventType}] Successfully processed message {MessageId}",
                eventTypeName, message.MessageId);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(message, messageActions, @event, ex, eventTypeName);
        }
    }

    /// <summary>
    /// Implementar la lógica de procesamiento específica del evento.
    /// </summary>
    protected abstract Task HandleAsync(TEvent @event);

    /// <summary>
    /// Obtener un identificador del evento para logging (ej: ReserveId, OrderId).
    /// </summary>
    protected virtual string GetEventIdentifier(TEvent @event) => string.Empty;

    private async Task HandleErrorAsync(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        TEvent @event,
        Exception ex,
        string eventTypeName)
    {
        var eventId = GetEventIdentifier(@event);
        var eventIdLog = string.IsNullOrEmpty(eventId) ? "" : $" [{eventId}]";

        Logger.LogError(ex,
            "[{EventType}]{EventId} Error processing message {MessageId}. Attempt {DeliveryCount}/{MaxRetries}",
            eventTypeName, eventIdLog, message.MessageId, message.DeliveryCount, MaxRetryAttempts);

        if (message.DeliveryCount >= MaxRetryAttempts)
        {
            Logger.LogWarning(
                "[{EventType}]{EventId} Max retry attempts reached for message {MessageId}. Sending to dead-letter.",
                eventTypeName, eventIdLog, message.MessageId);

            await messageActions.DeadLetterMessageAsync(
                message,
                deadLetterReason: "MaxRetriesExceeded",
                deadLetterErrorDescription: $"Failed after {MaxRetryAttempts} attempts. Last error: {ex.Message}");
        }
        else
        {
            Logger.LogInformation(
                "[{EventType}]{EventId} Abandoning message {MessageId} for retry. Next attempt will be {NextAttempt}/{MaxRetries}",
                eventTypeName, eventIdLog, message.MessageId, message.DeliveryCount + 1, MaxRetryAttempts);

            await messageActions.AbandonMessageAsync(message);
        }
    }
}
