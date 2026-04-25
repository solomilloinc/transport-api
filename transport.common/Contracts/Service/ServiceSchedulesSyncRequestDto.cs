namespace Transport.SharedKernel.Contracts.Service;

/// <summary>
/// Payload del endpoint bulk "sync" de schedules para un servicio
/// (<c>PUT /api/service-schedules-sync/{serviceId}</c>).
/// <para>
/// <b>Semántica declarativa (estado deseado)</b>: el cliente manda la lista completa
/// de schedules que el servicio debe tener después de la operación. El backend calcula
/// el diff contra la DB y aplica tres operaciones dentro de una única transacción:
/// <list type="bullet">
///   <item><b>Crear</b>: items con <c>ServiceScheduleId = null</c>.</item>
///   <item><b>Actualizar</b>: items con <c>ServiceScheduleId</c> existente — se pisan
///     <c>DepartureHour</c> e <c>IsHoliday</c>.</item>
///   <item><b>Soft-delete</b>: schedules activos en DB cuyo <c>ServiceScheduleId</c>
///     no aparece en el payload — se marcan con <c>Status = Deleted</c> para preservar
///     la integridad de reservas históricas que los referencien.</item>
/// </list>
/// </para>
/// <para>
/// <b>Por qué este diseño vs. endpoints individuales</b>: el flujo típico de edición
/// de la grilla horaria de un servicio implica varios cambios simultáneos (agregar uno,
/// ajustar otro, borrar un tercero). Con endpoints individuales el frontend tendría que
/// mantener un "diff local" y emitir N requests, lidiando con fallos parciales. Acá se
/// manda la lista completa, el backend se encarga del diff, y la transacción garantiza
/// "todo o nada". Los endpoints individuales siguen existiendo para casos puntuales
/// (pausar un horario, scripts, etc.).
/// </para>
/// <para>
/// <b>Idempotencia</b>: llamar dos veces con el mismo payload produce el mismo resultado.
/// </para>
/// </summary>
public record ServiceSchedulesSyncRequestDto(
    List<ServiceScheduleSyncItemDto> Schedules
);

/// <summary>
/// Un item de la lista de sync. Ver <see cref="ServiceSchedulesSyncRequestDto"/> para la
/// semántica general.
/// </summary>
/// <param name="ServiceScheduleId">
/// <c>null</c> → crear un schedule nuevo. Con valor → identifica un schedule existente
/// del mismo servicio para actualizar. Si el Id no existe o pertenece a otro servicio,
/// el backend rechaza toda la operación (no hay fallos parciales).
/// </param>
/// <param name="DepartureHour">Hora de salida del horario (&gt; 00:00).</param>
/// <param name="IsHoliday">Si el horario aplica en días feriados.</param>
public record ServiceScheduleSyncItemDto(
    int? ServiceScheduleId,
    TimeSpan DepartureHour,
    bool IsHoliday
);
