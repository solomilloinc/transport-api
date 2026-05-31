namespace Transport.SharedKernel.Contracts.Reserve;

/// <summary>
/// Respuesta del reporte de reservas por día: la página de reservas (filtrada por
/// <see cref="ReserveDayReportFilterDto.TripId"/> si vino) más el facet de Trips que tienen
/// reservas ese día para poblar el Select. El facet se calcula sobre el día completo, sin
/// importar el filtro de Trip, para que las opciones del Select no cambien al elegir una.
/// </summary>
public class ReserveDayReportResponseDto
{
    public PagedReportResponseDto<ReserveReportResponseDto> Reserves { get; set; } = new();
    public List<ReserveTripOptionDto> AvailableTrips { get; set; } = new();
}

/// <summary>Opción de Trip (ruta) para el Select de la página de Reservas.</summary>
public record ReserveTripOptionDto(int TripId, string Description);
