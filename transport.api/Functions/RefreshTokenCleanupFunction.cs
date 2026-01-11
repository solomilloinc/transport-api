using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Transport.Business.Authentication;

namespace transport_api.Functions;

public class RefreshTokenCleanupFunction
{
    private readonly ILogger<RefreshTokenCleanupFunction> _logger;
    private readonly ITokenProvider _tokenProvider;

    public RefreshTokenCleanupFunction(
        ILogger<RefreshTokenCleanupFunction> logger,
        ITokenProvider tokenProvider)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
    }

    // Ejecutar diariamente a las 3:00 AM UTC
    [Function("RefreshTokenCleanup")]
    public async Task Run([TimerTrigger("0 0 3 * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation(
            "Starting RefreshToken cleanup process at: {time}",
            DateTime.UtcNow
        );

        try
        {
            var startTime = DateTime.UtcNow;
            var deletedCount = await _tokenProvider.CleanupExpiredTokensAsync(daysOld: 30);
            var duration = DateTime.UtcNow - startTime;

            _logger.LogInformation(
                "RefreshToken cleanup completed successfully at: {time}. Deleted {count} expired tokens (duration: {durationMs}ms)",
                DateTime.UtcNow,
                deletedCount,
                duration.TotalMilliseconds
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during RefreshToken cleanup at: {time}", DateTime.UtcNow);
        }

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextRun}", myTimer.ScheduleStatus.Next);
        }
    }

    // Endpoint manual para testing o limpieza manual
    [Function("RefreshTokenCleanupManual")]
    public async Task<HttpResponseData> RunManual([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Manual RefreshToken cleanup triggered at: {time}", DateTime.UtcNow);

        try
        {
            var startTime = DateTime.UtcNow;
            var deletedCount = await _tokenProvider.CleanupExpiredTokensAsync(daysOld: 30);
            var duration = DateTime.UtcNow - startTime;

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteStringAsync($"Cleanup completed successfully. Deleted {deletedCount} expired tokens in {duration.TotalMilliseconds}ms");

            _logger.LogInformation(
                "Manual RefreshToken cleanup completed. Deleted {count} tokens (duration: {durationMs}ms)",
                deletedCount,
                duration.TotalMilliseconds
            );

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during manual RefreshToken cleanup");
            var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Exception: {ex.Message}");
            return response;
        }
    }
}
