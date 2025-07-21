using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using Transport.Business.Data;
using Transport.Business.Messaging;
using Transport.SharedKernel;
using Transport.SharedKernel.Configuration;

namespace Transport.Infraestructure.Messaging
{
    public class OutboxDispatcher: IOutboxDispatcher
    {
        private readonly IApplicationDbContext _dbContext;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ILogger<OutboxDispatcher> _logger;

        public OutboxDispatcher(
            IApplicationDbContext dbContext,
            ServiceBusClient serviceBusClient,
            ILogger<OutboxDispatcher> logger)
        {
            _dbContext = dbContext;
            _serviceBusClient = serviceBusClient;
            _logger = logger;
        }

        public async Task DispatchUnprocessedMessagesAsync(CancellationToken cancellationToken = default)
        {
            var messages = await _dbContext.OutboxMessages
                .Where(m => !m.Processed)
                .OrderBy(m => m.OccurredOn)
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(message.Topic))
                    {
                        _logger.LogWarning("Outbox message {MessageId} skipped due to missing topic.", message.Id);
                        continue;
                    }

                    var sender = _serviceBusClient.CreateSender(message.Topic);
                    var busMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(message.Content))
                    {
                        MessageId = message.Id.ToString(),
                        ContentType = "application/json"
                    };

                    await sender.SendMessageAsync(busMessage, cancellationToken);

                    message.Processed = true;
                    message.ProcessedOn = DateTime.UtcNow;

                    _dbContext.OutboxMessages.Update(message);
                    await _dbContext.SaveChangesWithOutboxAsync();

                    _logger.LogInformation("Outbox message {MessageId} sent to topic {Topic}.", message.Id, message.Topic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending Outbox message {MessageId}.", message.Id);
                }
            }

            await _dbContext.SaveChangesWithOutboxAsync(cancellationToken);
        }
    }
}
