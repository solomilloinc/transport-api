using Transport.SharedKernel;

namespace Transport.Infraestructure.Time;

internal sealed class DateTimeProvider : IDateTimeProvider
{
    // Zona horaria de operación: Argentina (UTC−3 fijo, sin horario de verano). Centralizada acá:
    // si algún día pasa a ser per-tenant o con DST, este es el único lugar a cambiar.
    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(-3);

    public DateTime UtcNow => DateTime.UtcNow;

    // local = UTC + offset (offset = −3). Kind=Unspecified: es un wall-clock local, no un instante UTC.
    public DateTime LocalNow => DateTime.SpecifyKind(DateTime.UtcNow + LocalOffset, DateTimeKind.Unspecified);
}
