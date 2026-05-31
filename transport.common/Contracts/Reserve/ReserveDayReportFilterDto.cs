namespace Transport.SharedKernel.Contracts.Reserve;

/// <summary>
/// Filtro del reporte de reservas por día (reserve-report/{date}).
/// <see cref="TripId"/> es opcional: null/0 ⇒ todas las reservas del día (página de Reservas);
/// con un valor ⇒ solo las reservas de ese <c>Trip</c> (Select por Travel, y pierna de vuelta
/// de un IdaVuelta donde el frontend manda el Trip inverso).
/// </summary>
public record ReserveDayReportFilterDto(int? TripId = null);
