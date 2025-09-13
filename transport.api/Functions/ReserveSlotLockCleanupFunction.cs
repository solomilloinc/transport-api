using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Transport.Domain.Reserves.Abstraction;
using Transport.SharedKernel.Configuration;

namespace transport_api.Functions;

public class ReserveSlotLockCleanupFunction
{
    private readonly ILogger<ReserveSlotLockCleanupFunction> _logger;
    private readonly IReserveBusiness _reserveBusiness;
    private readonly IReserveOption _reserveOptions;

    public ReserveSlotLockCleanupFunction(
        ILogger<ReserveSlotLockCleanupFunction> logger,
        IReserveBusiness reserveBusiness,
        IReserveOption reserveOptions)
    {
        _logger = logger;
        _reserveBusiness = reserveBusiness;
        _reserveOptions = reserveOptions;
    }

    // Timer configurable: por defecto cada minuto, pero se puede cambiar via configuración
    [Function("ReserveSlotLockCleanup")]
    public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
    {
        var cleanupInterval = _reserveOptions.SlotLockCleanupIntervalMinutes;

        _logger.LogInformation(
            "Starting ReserveSlotLock cleanup process at: {time} (configured interval: {intervalMinutes} minutes)",
            DateTime.UtcNow,
            cleanupInterval
        );

        try
        {
            var startTime = DateTime.UtcNow;
            var result = await _reserveBusiness.CleanupExpiredReserveSlotLocks();
            var duration = DateTime.UtcNow - startTime;

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "ReserveSlotLock cleanup completed successfully at: {time} (duration: {durationMs}ms)",
                    DateTime.UtcNow,
                    duration.TotalMilliseconds
                );

                // Log métricas adicionales si está disponible
                await LogCleanupMetrics(startTime, duration);
            }
            else
            {
                _logger.LogError("ReserveSlotLock cleanup failed: {error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during ReserveSlotLock cleanup at: {time}", DateTime.UtcNow);
        }

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextRun}", myTimer.ScheduleStatus.Next);
        }
    }

    // Manual cleanup endpoint (útil para testing o limpieza manual)
    [Function("ReserveSlotLockCleanupManual")]
    public async Task<HttpResponseData> RunManual([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Manual ReserveSlotLock cleanup triggered at: {time}", DateTime.UtcNow);

        try
        {
            var startTime = DateTime.UtcNow;
            var result = await _reserveBusiness.CleanupExpiredReserveSlotLocks();
            var duration = DateTime.UtcNow - startTime;

            var response = req.CreateResponse(result.IsSuccess ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.InternalServerError);

            if (result.IsSuccess)
            {
                await response.WriteStringAsync($"Cleanup completed successfully in {duration.TotalMilliseconds}ms");
                _logger.LogInformation("Manual cleanup completed successfully (duration: {durationMs}ms)", duration.TotalMilliseconds);
            }
            else
            {
                await response.WriteStringAsync($"Cleanup failed: {result.Error}");
                _logger.LogError("Manual cleanup failed: {error}", result.Error);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during manual cleanup");
            var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Exception: {ex.Message}");
            return response;
        }
    }

    private async Task LogCleanupMetrics(DateTime startTime, TimeSpan duration)
    {
        try
        {
            // Aquí podrías agregar métricas específicas como:
            // - Número de locks limpiados
            // - Tiempo promedio de limpieza
            // - Estado de la base de datos
            // Por ahora solo log básico

            _logger.LogInformation(
                "Cleanup metrics - StartTime: {startTime}, Duration: {duration}, ConfiguredInterval: {interval}min",
                startTime,
                duration,
                _reserveOptions.SlotLockCleanupIntervalMinutes
            );

            // Ejemplo: Si la limpieza toma más tiempo del configurado, log warning
            var maxExpectedDuration = TimeSpan.FromSeconds(30); // 30 segundos máximo esperado
            if (duration > maxExpectedDuration)
            {
                _logger.LogWarning(
                    "Cleanup took longer than expected. Duration: {duration}ms, Expected: <{expectedMs}ms",
                    duration.TotalMilliseconds,
                    maxExpectedDuration.TotalMilliseconds
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log cleanup metrics");
        }
    }
}