using Transport.SharedKernel;

namespace Transport.Domain.FrequentSubscriptions.Abstraction;

public interface IFrequentPassengerBusiness
{
    /// <summary>
    /// Procesa todas las suscripciones activas. Pensado para el batch scheduled.
    /// </summary>
    Task<Result<bool>> GenerateFrequentPassengersAsync();

    /// <summary>
    /// Procesa una suscripción puntual. Pensado para auto-apply inmediato después de Create:
    /// el admin crea la sub y los Passengers aparecen en segundos sobre las Reserves ya existentes
    /// de la ventana, sin tener que disparar el batch entero.
    /// Idempotente — re-ejecutarlo no genera duplicados.
    /// </summary>
    Task<Result<bool>> GenerateForSubscriptionAsync(int frequentSubscriptionId);
}
