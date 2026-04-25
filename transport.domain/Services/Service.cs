using Transport.Domain.Cities;
using Transport.Domain.Reserves;
using Transport.Domain.Trips;
using Transport.Domain.Vehicles;
using Transport.SharedKernel;

namespace Transport.Domain.Services;
public class Service : Entity, IAuditable, ITenantScoped
{
    public int ServiceId { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = null!;
    public int TripId { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public int VehicleId { get; set; }

    /// <summary>
    /// Día de inicio del rango semanal en que el servicio opera, siguiendo la
    /// convención de <see cref="System.DayOfWeek"/> (<c>Sunday = 0</c> … <c>Saturday = 6</c>).
    /// Se evalúa junto con <see cref="EndDay"/> en <see cref="IsDayWithinScheduleRange"/>
    /// para decidir en qué fechas concretas genera reservas
    /// <c>ServiceBusiness.GenerateFutureReservesAsync</c>.
    ///
    /// <para>
    /// <b>Modelo de rango contiguo con wraparound</b> — la decisión de diseño:
    /// <list type="bullet">
    ///   <item><c>Start == End</c> → solo ese día de la semana.</item>
    ///   <item><c>Start &lt; End</c> → rango inclusivo sin cruzar el domingo
    ///     (ej. <c>1,5</c> = Lun–Vie; <c>1,0</c> = Lun–Dom = todos los días).</item>
    ///   <item><c>Start &gt; End</c> → rango con wraparound (ej. <c>5,1</c> = Vie–Lun
    ///     cruzando Sáb y Dom, típico de servicios de fin de semana).</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Por qué rango contiguo y no bitmask, tabla 1:N u otras alternativas:</b>
    /// <list type="bullet">
    ///   <item>Cubre el 100% de los casos operativos reales del negocio de transporte
    ///     regular (rutas L–V, todos los días, solo fines de semana).</item>
    ///   <item>Una sola expresión booleana resuelve la pertenencia; sin joins, sin
    ///     parsing, sin tabla hija, sin bit-test.</item>
    ///   <item>2 columnas tinyint en DB, leíbles en un SELECT crudo.</item>
    ///   <item>Si en el futuro aparece un caso de días <b>no contiguos</b>
    ///     (ej. Lun-Mié-Vie alternados), la migración a bitmask <c>[Flags] enum</c>
    ///     es un cambio localizado: entidad + DTO + helper + script de backfill.
    ///     No se pierde nada por empezar con el modelo de rango.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Historial del bug del default silencioso:</b> cuando <see cref="DayOfWeek"/>
    /// no se asigna explícitamente toma su valor default <c>Sunday (0)</c>. Antes de
    /// exponer estos campos en los DTOs de Create/Update, todo servicio nuevo se
    /// persistía con <c>StartDay=0, EndDay=0</c>, lo que el helper interpreta como
    /// "solo domingos". Ese fue el bug que afectó a los servicios 1, 2, 1002, 1003
    /// y motivó esta documentación. La corrección fue hacer los campos obligatorios
    /// en el DTO — ver <c>ServiceCreateRequestDto</c>.
    /// </para>
    /// </summary>
    public DayOfWeek StartDay { get; set; }

    /// <summary>
    /// Día de fin del rango semanal en que el servicio opera. Ver la documentación
    /// completa en <see cref="StartDay"/> (semántica del rango, wraparound, y
    /// justificación de diseño).
    /// </summary>
    public DayOfWeek EndDay { get; set; }

    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Trip Trip { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;

    public ICollection<ServiceCustomer> Customers { get; set; } = new List<ServiceCustomer>();
    public ICollection<Reserve> Reserves { get; set; } = new List<Reserve>();
    public ICollection<ServiceSchedule> Schedules { get; set; } = new List<ServiceSchedule>();
    public ICollection<ServiceDirection> AllowedDirections { get; set; } = new List<ServiceDirection>();

    /// <summary>
    /// Determina si un <see cref="DayOfWeek"/> cae dentro del rango operativo
    /// <c>[StartDay, EndDay]</c> del servicio. Ver <see cref="StartDay"/> para la
    /// descripción completa del modelo.
    /// </summary>
    /// <remarks>
    /// Maneja los tres casos del modelo en orden de especificidad:
    /// <list type="number">
    ///   <item><b>Un solo día</b> (<c>Start == End</c>): igualdad estricta.</item>
    ///   <item><b>Rango sin wrap</b> (<c>Start &lt; End</c>): comparación directa.</item>
    ///   <item><b>Rango con wraparound</b> (<c>Start &gt; End</c>): el día cae dentro
    ///     si está en el tramo <c>[Start, 6]</c> <b>o</b> en <c>[0, End]</c>.
    ///     Ejemplo: Start=Fri(5), End=Mon(1) → operativo {Fri, Sat, Sun, Mon} =
    ///     {5, 6, 0, 1}.</item>
    /// </list>
    /// </remarks>
    public bool IsDayWithinScheduleRange(DayOfWeek day)
    {
        // Caso 1: rango de un solo día.
        if (StartDay == EndDay)
            return day == StartDay;

        // Caso 2: rango sin wrap (Start y End en el mismo "lado" de la semana).
        if (StartDay < EndDay)
            return day >= StartDay && day <= EndDay;

        // Caso 3: rango con wraparound — cruza Sábado/Domingo.
        // Ej.: Start=Fri(5), End=Mon(1) → true para Vie/Sáb (>= 5) o Dom/Lun (<= 1).
        return day >= StartDay || day <= EndDay;
    }
}

