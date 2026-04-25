namespace Transport.SharedKernel.Contracts.Service;

/// <summary>
/// Payload para actualizar un <c>Service</c> existente (endpoint PUT
/// <c>service-update/{serviceId}</c>).
/// <para>
/// <b>StartDay / EndDay</b>: ver la documentación de <see cref="ServiceCreateRequestDto"/>
/// para la semántica completa del rango (incluyendo wraparound) y la justificación de
/// por qué son obligatorios. Resumen rápido:
/// <list type="bullet">
///   <item>Enteros 0-6 (<see cref="DayOfWeek"/>: Sunday=0 … Saturday=6).</item>
///   <item><c>1,5</c> = Lun–Vie. <c>5,1</c> = Vie–Lun con wrap. <c>3,3</c> = solo Miércoles.</item>
/// </list>
/// </para>
/// <para>
/// <b>TripId</b>: es editable. Casos de uso reales:
/// <list type="bullet">
///   <item>Corrección de configuración inicial (se creó el servicio apuntando al Trip equivocado).</item>
///   <item>Ajuste de ruta por cambio operativo (nueva parada intermedia, cambio de ciudad destino).</item>
///   <item>Migración a un Trip nuevo cuando el viejo se deprecó pero el servicio sigue vigente.</item>
/// </list>
/// <b>Qué pasa con las reservas ya existentes al cambiar el TripId:</b> cada <c>Reserve</c>
/// guarda su propio <c>TripId</c> al momento de crearse (lo copia del Service en
/// <c>GenerateFutureReservesAsync</c>). Las reservas históricas y las ya generadas a
/// futuro quedan intactas con el TripId viejo. Solo las nuevas reservas que genere el
/// batch usarán el TripId nuevo. Esto preserva la integridad contable/contractual.
/// </para>
/// <para>
/// <b>Qué NO incluye este DTO respecto al de creación:</b>
/// <list type="bullet">
///   <item><c>Schedules</c>: los <c>ServiceSchedule</c> tienen su propio ciclo de vida vía
///     los endpoints <c>service-schedule-create</c>, <c>service-schedule-update</c>,
///     <c>service-schedule-delete</c>, <c>service-schedule-status</c>.
///     Mezclarlos acá forzaba a soft-deletear y recrear todos los schedules en cada update
///     del servicio, invalidando reservas ya ligadas. Se separa por claridad y seguridad.</item>
/// </list>
/// </para>
/// </summary>
public record ServiceUpdateRequestDto(
    string Name,
    int TripId,
    TimeSpan EstimatedDuration,
    int VehicleId,
    DayOfWeek StartDay,
    DayOfWeek EndDay,
    List<int>? AllowedDirectionIds = null
);
