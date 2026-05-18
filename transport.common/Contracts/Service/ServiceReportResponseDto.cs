namespace Transport.SharedKernel.Contracts.Service;

public record ServiceReportResponseDto(
    int ServiceId,
    string Name,
    int TripId,
    string TripDescription,
    int OriginId,
    string OriginName,
    int DestinationId,
    string DestinationName,
    DayOfWeek DayOfWeek,
    TimeSpan DepartureHour,
    TimeSpan EstimatedDuration,
    bool IsHoliday,
    ServiceVehicleResponseDto Vehicle,
    string Status,
    List<ServiceDirectionResponseDto> AllowedDirections)
{
    /// <summary>
    /// Nombre del día en español, derivado de <see cref="DayOfWeek"/>. Computed — no es
    /// parte del constructor ni se persiste; se calcula en cada serialización.
    /// </summary>
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

public record ServiceDirectionResponseDto(
    int DirectionId,
    string Name,
    int CityId);
