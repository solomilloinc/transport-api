namespace Transport.SharedKernel;

public interface IDateTimeProvider
{
    /// <summary>Instante actual en UTC. Comparar contra esto los datos UTC (auditoría, expiraciones, webhooks).</summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// "Ahora" en hora local de operación (Argentina, UTC−3 fijo, sin horario de verano).
    /// Es el único punto donde se cruza UTC→local. Comparar contra esto los datos de **agenda**
    /// (p. ej. <c>ReserveDate.Date + DepartureHour</c>), que se guardan en hora local;
    /// nunca compararlos contra <see cref="UtcNow"/> directamente.
    /// </summary>
    DateTime LocalNow { get; }
}
