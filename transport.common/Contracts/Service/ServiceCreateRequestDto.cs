namespace Transport.SharedKernel.Contracts.Service;

/// <summary>
/// Payload para crear un <c>Service</c> (servicio recurrente de transporte).
/// <para>
/// <b>StartDay / EndDay</b>: definen el rango de días de la semana en los que el servicio
/// opera, siguiendo la convención de <see cref="System.DayOfWeek"/> donde
/// <c>Sunday = 0</c>, <c>Monday = 1</c>, …, <c>Saturday = 6</c>.
/// </para>
/// <para>
/// Ambos campos son <b>obligatorios</b>. La interpretación del rango (implementada en
/// <c>Service.IsDayWithinScheduleRange</c>) es la siguiente:
/// <list type="bullet">
///   <item><c>StartDay == EndDay</c> → solo ese día de la semana.</item>
///   <item><c>StartDay &lt; EndDay</c> → rango inclusivo sin wrap (p. ej. <c>1,5</c> = Lun–Vie).</item>
///   <item><c>StartDay &gt; EndDay</c> → rango con wraparound cruzando el domingo
///     (p. ej. <c>5,1</c> = Viernes, Sábado, Domingo, Lunes — servicio de fin de semana).</item>
/// </list>
/// </para>
/// <para>
/// <b>Por qué son obligatorios (no opcionales, no nullable):</b> el default implícito del
/// tipo <see cref="DayOfWeek"/> es <c>Sunday (0)</c>. Cuando estos campos estaban ausentes
/// del DTO, los servicios nuevos quedaban persistidos con <c>StartDay=0, EndDay=0</c>, lo
/// que el helper interpretaba como "solo domingo". Este default silencioso generó el bug
/// real en los servicios 1, 2, 1002 y 1003 (solo se creaban reservas los domingos). Forzar
/// al cliente a enviar ambos valores explícitamente hace imposible que vuelva a pasar.
/// </para>
/// <para>
/// <b>Serialización en la API:</b> <see cref="DayOfWeek"/> viaja como entero (0-6) con
/// <c>System.Text.Json</c> por defecto. No se agrega <c>JsonStringEnumConverter</c> para
/// mantener el contrato consistente con el resto de los enums del dominio, que ya se
/// serializan como int.
/// </para>
/// </summary>
public record ServiceCreateRequestDto(
    string Name,
    int TripId,
    TimeSpan EstimatedDuration,
    int VehicleId,
    DayOfWeek StartDay,
    DayOfWeek EndDay,
    List<ServiceScheduleCreateDto> Schedules,
    List<int>? AllowedDirectionIds = null);
