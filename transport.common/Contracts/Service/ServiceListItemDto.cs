namespace Transport.SharedKernel.Contracts.Service;

/// <summary>
/// Shape para dropdowns y formularios (ej: form de FrequentSubscription). Incluye
/// metadata suficiente para que el frontend filtre por trip inverso, día, hora, y
/// pickup/dropoff permitidos sin tener que pedir el reporte paginado.
/// </summary>
public record ServiceListItemDto(
    int ServiceId,
    string Name,
    int TripId,
    string TripDescription,
    int OriginCityId,
    int DestinationCityId,
    DayOfWeek DayOfWeek,
    TimeSpan DepartureHour,
    List<int> AllowedDirectionIds)
{
    public string DayOfWeekName => DayOfWeek switch
    {
        DayOfWeek.Sunday    => "Domingo",
        DayOfWeek.Monday    => "Lunes",
        DayOfWeek.Tuesday   => "Martes",
        DayOfWeek.Wednesday => "Miércoles",
        DayOfWeek.Thursday  => "Jueves",
        DayOfWeek.Friday    => "Viernes",
        DayOfWeek.Saturday  => "Sábado",
        _ => DayOfWeek.ToString()
    };
}
