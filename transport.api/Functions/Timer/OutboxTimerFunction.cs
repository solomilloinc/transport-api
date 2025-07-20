using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Transport.Business.Messaging;

namespace transport_api.Functions.Timer;

public class OutboxTimerFunction
{
    private readonly IOutboxDispatcher _dispatcher;
    private readonly ILogger<OutboxTimerFunction> _logger;

    public OutboxTimerFunction(IOutboxDispatcher dispatcher, ILogger<OutboxTimerFunction> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [Function("OutboxTimerFunction")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);
        
        if (myTimer.ScheduleStatus is not null)
        {
            await _dispatcher.DispatchUnprocessedMessagesAsync();
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }
    }
}